using ADPUK.NINA.AddToAlignmentModel.Locales;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ADPUK.NINA.AddToAlignmentModel {
    public partial class ModelPointCreator {

        private ICameraMediator cameraMediator;
        private ITelescopeMediator telescopeMediator;
        private IRotatorMediator rotatorMediator;
        private IImagingMediator imagingMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IPlateSolverFactory plateSolverFactory;
        private IWindowServiceFactory windowServiceFactory;
        private IProfileService profileService;
        private PlateSolvingStatusVM PlateSolveStatusVM;
        private IWindowService service;



        private DeviceUpdateTimer updateTimer;

        public ModelPointCreator(
            ICameraMediator cameraMediator,
            ITelescopeMediator telescopeMediator,
            IRotatorMediator rotatorMediator,
            IImagingMediator imagingMediator,
            IFilterWheelMediator filterWheelMediator,
            IPlateSolverFactory plateSolverFactory,
            IWindowServiceFactory windowServiceFactory,
            IProfileService profileService) {
            this.cameraMediator = cameraMediator;
            this.telescopeMediator = telescopeMediator;
            this.rotatorMediator = rotatorMediator;
            this.imagingMediator = imagingMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.profileService = profileService;
            PlateSolveStatusVM = new PlateSolvingStatusVM();
        }

        public async Task<PlateSolveResult> SolveDirectToMount(
            Coordinates expectedCentre,
            int solveAttempts,
            int plateSolveCloseDelay,
            IProgress<ApplicationStatus> progress,
            CancellationToken token,
            bool showDialog = true) {

            try {
                service = windowServiceFactory.Create();
                if (showDialog) {
                    service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
                }

                PlateSolveResult result = await DoSolve(expectedCentre, progress, solveAttempts, token);
                if (result.Success) {
                    telescopeMediator.Action("Telescope:AddAlignmentReference", $"{result.Coordinates.RA}:{result.Coordinates.Dec}");
                }

                return result;
            } finally {
                service.DelayedClose(new TimeSpan(0, 0, plateSolveCloseDelay));
            }
        }

        public async Task<ModelPoint> GetCurrentLocation(int solveAttempts, int plateSolveCloseDelay, IProgress<ApplicationStatus> progress, CancellationToken token, bool showDialog = true) {
            Coordinates currentPosition = telescopeMediator.GetCurrentPosition();
            progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
            service = windowServiceFactory.Create();
            ModelPoint modelPoint = new ModelPoint(currentPosition, null, telescopeMediator.GetInfo().EquatorialSystem);
            if (showDialog) {
                service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
            }
            PlateSolveResult result = await DoSolve(currentPosition, progress, solveAttempts, token);
            service.DelayedClose(new TimeSpan(0, 0, plateSolveCloseDelay));
            if (!result.Success) {
                modelPoint.Status = ViewStrings.PlateSolveFailed;
                Notification.ShowWarning($"{ViewStrings.PlateSolveFailedRADec.Replace("{{RA}}",
                    currentPosition.RAString).Replace("{{Dec}}}",
                    currentPosition.DecString)}");
                return modelPoint;
            } else {
                Coordinates resultCoordinates = result.Coordinates.Transform(telescopeMediator.GetInfo().EquatorialSystem);
                string addAlignmentResponse = telescopeMediator.Action("Telescope:AddAlignmentReference", $"{resultCoordinates}");
                modelPoint.Status = "OK";
                modelPoint.ActualCoordinates = resultCoordinates;
                return modelPoint;
            }
        }
        public async Task<ModelPoint> CreateModelPoint(ModelCreationParameters creationParameters, IProgress<ApplicationStatus> progress, CancellationToken token, bool showDialog = true) {
            ModelPoint modelPoint = new ModelPoint(creationParameters.TargetCoordinates, null, telescopeMediator.GetInfo().EquatorialSystem);
            progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
            Coordinates target = creationParameters.TargetCoordinates.Transform(telescopeMediator.GetInfo().EquatorialSystem);
            var scopeInfo = telescopeMediator.GetInfo();
            try {
                service = windowServiceFactory.Create();
                if (ADP_Tools.AboveMinAlt(
                         creationParameters.TargetCoordinates,
                         profileService.ActiveProfile.AstrometrySettings.Horizon,
                         telescopeMediator.GetInfo().SiteLatitude,
                         creationParameters.MinElevationAboveHorizon)) {

                    await telescopeMediator.SlewToCoordinatesAsync(target, token);
                    if (cameraMediator.GetInfo().Connected) {
                        if (showDialog) {
                            service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
                        }
                        PlateSolveResult result = await DoSolve(target, progress, creationParameters.SolveAttempts, token);
                        var topoCoords = creationParameters.TargetCoordinates.Transform(
                            Angle.ByDegree(scopeInfo.SiteLatitude),
                            Angle.ByDegree(scopeInfo.SiteLongitude),
                            scopeInfo.SiteElevation);
                        if (!result.Success) {
                            modelPoint.Status = ViewStrings.PlateSolveFailed;
                            Notification.ShowWarning($"{ViewStrings.PlateSolveFailedAt.Replace("{{Azimuth}}",
                                AstroUtil.DegreesToHMS(topoCoords.Azimuth.Degree)).Replace("{{Altitude}}}",
                                AstroUtil.DegreesToDMS(topoCoords.Altitude.Degree))}");
                            return modelPoint;
                        } else {
                            Coordinates resultCoordinates = result.Coordinates.Transform(telescopeMediator.GetInfo().EquatorialSystem);
                            string addAlignmentResponse = telescopeMediator.Action("Telescope:AddAlignmentReference", $"{resultCoordinates.RA}:{resultCoordinates.Dec}");
                            TimeSpan waitTime = TimeSpan.FromSeconds(profileService.ActiveProfile.ApplicationSettings.DevicePollingInterval);
                            await Task.Delay(waitTime, token);
                            modelPoint.Status = "OK";
                            modelPoint.ActualCoordinates = resultCoordinates;
                            return modelPoint;
                        }
                    } else {
                        modelPoint.Status = Loc.Instance["Lbl_CameraNotConnected"];
                        Notification.ShowWarning(Loc.Instance["Lbl_CameraNotConnected"]);
                        return modelPoint;
                    }
                } else {
                    Notification.ShowWarning($"{ViewStrings.TargetBelowHorizon
                        .Replace("{{Azimuth}}", AstroUtil.DegreesToHMS(creationParameters.TargetCoordinates.Transform(Angle.ByDegree(scopeInfo.SiteLatitude), Angle.ByDegree(scopeInfo.SiteLongitude)).Azimuth.Degree))
                        .Replace("{{Altitude}}", AstroUtil.DegreesToDMS(creationParameters.TargetCoordinates.Transform(Angle.ByDegree(scopeInfo.SiteLatitude), Angle.ByDegree(scopeInfo.SiteLongitude)).Altitude.Degree))}");
                    return modelPoint;
                }
            } finally {
                service.DelayedClose(new TimeSpan(0, 0, creationParameters.PlateSolveCloseDelay));
            }
        }


        public virtual async Task<PlateSolveResult> DoSolve(Coordinates expectedCentre, IProgress<ApplicationStatus> progress, int solveAttempts, CancellationToken token) {
            IPlateSolver plateSolver = plateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);
            IPlateSolver blindSolver = plateSolverFactory.GetBlindSolver(profileService.ActiveProfile.PlateSolveSettings);

            ICaptureSolver solver = plateSolverFactory.GetCaptureSolver(plateSolver, blindSolver, imagingMediator, filterWheelMediator);

            CaptureSolverParameter parameter = ADP_Tools.CreateCaptureSolverParameter(profileService.ActiveProfile, expectedCentre, solveAttempts);

            CaptureSequence seq = new CaptureSequence(
                profileService.ActiveProfile.PlateSolveSettings.ExposureTime,
                CaptureSequence.ImageTypes.SNAPSHOT,
                profileService.ActiveProfile.PlateSolveSettings.Filter,
                new BinningMode(profileService.ActiveProfile.PlateSolveSettings.Binning, profileService.ActiveProfile.PlateSolveSettings.Binning),
                1
            );
            return await solver.Solve(seq, parameter, PlateSolveStatusVM.Progress, progress, token);
        }
        [JsonObject(MemberSerialization.OptIn)]
        public partial class ModelCreationParameters : ObservableObject {
            [ObservableProperty]
            [JsonProperty("MinElevationAboveHorizon")]
            private double _MinElevationAboveHorizon;
            [ObservableProperty]
            [JsonProperty("MinElevation")]
            private double _MinElevation;
            [ObservableProperty]
            [JsonProperty("MaxElevation")]
            private double _MaxElevation;
            [ObservableProperty]
            [JsonProperty("NumberOfAltitudePoints")]
            private int _NumberOfAltitudePoints;
            [ObservableProperty]
            [JsonProperty("NumberOfAzimuthPoints")]
            private int _NumberOfAzimuthPoints;
            [ObservableProperty]
            [JsonProperty("SolveAttempts")]
            private int _SolveAttempts;
            [ObservableProperty]
            [JsonProperty("PlateSolveDelay")]
            private int _PlateSolveCloseDelay;
            [JsonIgnore]
            public Coordinates TargetCoordinates;
            [JsonIgnore]
            public double AltStepSize {
                get {
                    double altStep = 0;
                    if (Math.Abs(MaxElevation - MinElevation) < 5) NumberOfAltitudePoints = 1;
                    if (NumberOfAltitudePoints > 1) {
                        altStep = ((MaxElevation - MinElevation) / (NumberOfAltitudePoints - 1));
                    } else {
                        MinElevation = ((MinElevation + MaxElevation) / 2d);
                        altStep = (MaxElevation + MinElevation) / 2d;
                    }
                    return altStep;
                }
            }
            public double AzStepSize {
                get {
                    return (360d / NumberOfAzimuthPoints);

                }
            }
        }

    }
}