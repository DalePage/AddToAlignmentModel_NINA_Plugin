using ADPUK.NINA.AddToAlignmentModel.Locales;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
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
            int solveAttempts,
            int plateSolveCloseDelay,
            IProgress<ApplicationStatus> progress,
            CancellationToken token,
            bool showDialog = true) {

            try {
                if (showDialog) {
                    service = windowServiceFactory.Create();
                    service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
                }

                PlateSolveResult result = await DoSolve(progress, solveAttempts, token);
                if (result.Success) {
                    telescopeMediator.Action("Telescope:AddAlignmentReference", $"{result.Coordinates.RA}:{result.Coordinates.Dec}");
                }

                return result;
            } finally {
                service.DelayedClose(new TimeSpan(0, 0, plateSolveCloseDelay));
            }
        }

        public async Task<ModelPoint> CreateModelPoint(ModelCreationParameters creationParameters, IProgress<ApplicationStatus> progress, CancellationToken token, bool showDialog = true) {
            ModelPoint modelPoint = new ModelPoint(creationParameters.TargetCoordinatesAltAz);
            progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
            Coordinates target = creationParameters.TargetCoordinatesAltAz.Transform(Epoch.JNOW);
            try {
                if (ADP_Tools.AboveMinAlt(
                        creationParameters.TargetCoordinatesAltAz,
                        profileService.ActiveProfile.AstrometrySettings.Horizon,
                        creationParameters.MinElevationAboveHorizon)) {

                    await telescopeMediator.SlewToCoordinatesAsync(target, token);
                    if (cameraMediator.GetInfo().Connected) {
                        if (showDialog) {
                            service = windowServiceFactory.Create();
                            service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
                        }
                        PlateSolveResult result = await DoSolve(progress, creationParameters.SolveAttempts, token);
                        if (showDialog) { service.DelayedClose(new TimeSpan(0, 0, creationParameters.PlateSolveCloseDelay)); }
                        if (!result.Success) {
                            modelPoint.ActualRAString = ViewStrings.PlateSolveFailed;
                            Notification.ShowWarning($"{ViewStrings.PlateSolveFailedAt.Replace("{{Azimuth}}",
                                creationParameters.TargetCoordinatesAltAz.Azimuth.ToString()).Replace("{{Altitude}}}",
                                creationParameters.TargetCoordinatesAltAz.Altitude.ToString())}");
                            return modelPoint;
                        } else {
                            Coordinates resultCoordinates = result.Coordinates.Transform(Epoch.JNOW);
                            string addAlignmentResponse = telescopeMediator.Action("Telescope:AddAlignmentReference", $"{resultCoordinates.RA}:{resultCoordinates.Dec}");
                            modelPoint = new ModelPoint(creationParameters.TargetCoordinatesAltAz, result);
                            return modelPoint;
                        }
                    } else {
                        modelPoint.ActualRAString = Loc.Instance["Lbl_CameraNotConnected"];
                        Notification.ShowWarning(Loc.Instance["Lbl_CameraNotConnected"]);
                        return modelPoint;
                    }
                } else {
                    Notification.ShowWarning($"{ViewStrings.TargetBelowHorizon
                        .Replace("{{Azimuth}}", creationParameters.TargetCoordinatesAltAz.Azimuth.ToString())
                        .Replace("{{Altitude}}", creationParameters.TargetCoordinatesAltAz.Altitude.ToString())}");
                    modelPoint.ActualRAString = ViewStrings.TargetBelowHorizon;
                    return modelPoint;
                }
            } finally {
                service.DelayedClose(new TimeSpan(0, 0, creationParameters.PlateSolveCloseDelay));
            }
        }


        public virtual async Task<PlateSolveResult> DoSolve(IProgress<ApplicationStatus> progress, int solveAttempts, CancellationToken token) {
            IPlateSolver plateSolver = plateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);
            IPlateSolver blindSolver = plateSolverFactory.GetBlindSolver(profileService.ActiveProfile.PlateSolveSettings);

            ICaptureSolver solver = plateSolverFactory.GetCaptureSolver(plateSolver, blindSolver, imagingMediator, filterWheelMediator);

            CaptureSolverParameter parameter = ADP_Tools.CreateCaptureSolverParameter(profileService.ActiveProfile, telescopeMediator.GetCurrentPosition(), solveAttempts);

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
            public TopocentricCoordinates TargetCoordinatesAltAz;
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