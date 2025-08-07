using ADPUK.NINA.AddToAlignmentModel.Locales;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ADPUK.NINA.AddToAlignmentModel {
    public partial class ModelCreation {

        private ICameraMediator cameraMediator;
        private ITelescopeMediator telescopeMediator;
        private IRotatorMediator rotatorMediator;
        private IImagingMediator imagingMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IPlateSolverFactory plateSolverFactory;
        private IWindowServiceFactory windowServiceFactory;
        private IProfileService profileService;
        private PluginOptionsAccessor pluginSettings;
        private PlateSolvingStatusVM PlateSolveStatusVM { get; } = new PlateSolvingStatusVM();



        public ModelCreation(
            ICameraMediator cameraMediator,
            ITelescopeMediator telescopeMediator,
            IRotatorMediator rotatorMediator,
            IImagingMediator imagingMediator,
            IFilterWheelMediator filterWheelMediator,
            IPlateSolverFactory plateSolverFactory,
            IWindowServiceFactory windowServiceFactory,
            IProfileService profileService,
            PluginOptionsAccessor pluginSettings) {

            this.cameraMediator = cameraMediator;
            this.telescopeMediator = telescopeMediator;
            this.rotatorMediator = rotatorMediator;
            this.imagingMediator = imagingMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.pluginSettings = pluginSettings;
            this.profileService = profileService;
        }
        public async Task ExecuteCreate(IProgress<ApplicationStatus> progress, CancellationToken token) {
            int StepCount = 0;
            IWindowService service = windowServiceFactory.Create();
            TelescopeInfo telescopeInfo = telescopeMediator.GetInfo();
            progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
            try {
                double altStep = 0;
                if (Math.Abs(MaxElevation - passedParams.MinElevation) < 5) passedParams.NumberOfAltitudePoints = 1;
                if (passedParams.NumberOfAltitudePoints > 1) {
                    altStep = ((passedParams.MaxElevation - passedParams.MinElevation) / (passedParams.NumberOfAltitudePoints - 1));
                } else {
                    passedParams.MinElevation = ((passedParams.MinElevation + passedParams.MaxElevation) / 2.0);
                    altStep = (passedParams.MaxElevation + passedParams.MinElevation) / 2.0;
                }
                double azStep = (360.0 / passedParams.NumberOfAzimuthPoints);
                TopocentricCoordinates altAzTarget;
                double initialAzimuth = ADP_Tools.ReadyToStart(telescopeInfo);
                double targetAz = initialAzimuth;
                StepCount = 0;
                double nextAz = initialAzimuth;
                for (int azc = 1; azc <= passedParams.NumberOfAzimuthPoints; azc++) {
                    targetAz = nextAz < 360.0 ? nextAz : nextAz - 360.0;
                    for (double nextAlt = passedParams.MinElevation; nextAlt <= passedParams.MaxElevation; nextAlt += altStep) {
                        if (IsPaused) { await pauseTask(); }
                        ModelPoint modelPoint;
                        service.DelayedClose(new TimeSpan(0, 0, passedParams.PlateSolveCloseDelay));
                        altAzTarget = new TopocentricCoordinates(
                            Angle.ByDegree(targetAz),
                            Angle.ByDegree(nextAlt),
                            Angle.ByDegree(telescopeInfo.SiteLatitude),
                            Angle.ByDegree(telescopeInfo.SiteLongitude)
                            );
                        if (ADP_Tools.AboveMinAlt(
                                altAzTarget,
                                profileService.ActiveProfile.AstrometrySettings.Horizon,
                                passedParams.MinElevationAboveHorizon)) {

                            await telescopeMediator.SlewToCoordinatesAsync(altAzTarget.Transform(Epoch.JNOW), token);
                            service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
                            if (cameraMediator.GetInfo().Connected) {
                                if (IsPaused) { await pauseTask(); }
                                PlateSolveResult result = await DoSolve(progress, token);
                                if (!result.Success) {
                                    modelPoint = new ModelPoint(altAzTarget);
                                    modelPoint.ActualRAString = ViewStrings.PlateSolveFailed;
                                    ModelPoints.Add(modelPoint);
                                    Notification.ShowWarning($"{ViewStrings.PlateSolveFailedAt.Replace("{{Azimuth}}", targetAz.ToString()).Replace("{{Altitude}}}", targetAz.ToString())}");
                                } else {
                                    Coordinates resultCoordinates = result.Coordinates.Transform(Epoch.JNOW);
                                    string addAlignmentResponse = telescopeMediator.Action("Telescope:AddAlignmentReference", $"{resultCoordinates.RA}:{resultCoordinates.Dec}");
                                    modelPoint = new ModelPoint(altAzTarget, result);
                                    ModelPoints.Add(modelPoint);
                                }
                            } else {
                                modelPoint = new ModelPoint(altAzTarget);
                                modelPoint.ActualRAString = Loc.Instance["Lbl_CameraNotConnected"];
                                ModelPoints.Add(modelPoint);
                                Notification.ShowWarning(Loc.Instance["Lbl_CameraNotConnected"]);
                            }
                        } else {
                            Notification.ShowWarning($"{ViewStrings.TargetBelowHorizon.Replace("{{Azimuth}}", targetAz.ToString()).Replace("{{Altitude}}", nextAlt.ToString())}");
                        }
                        StepCount++;
                    }
                    nextAz += azStep;
                }

            } catch (OperationCanceledException) {
                Notification.ShowWarning(ViewStrings.ModelBuilderCancelled);
            } finally {
                service.DelayedClose(new TimeSpan(0, 0, passedParams.PlateSolveCloseDelay));
            }

        }

    }

            protected virtual async Task<PlateSolveResult> DoSolve(IProgress<ApplicationStatus> progress, CancellationToken token) {
            IPlateSolver plateSolver = plateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);
            IPlateSolver blindSolver = plateSolverFactory.GetBlindSolver(profileService.ActiveProfile.PlateSolveSettings);

            ICaptureSolver solver = plateSolverFactory.GetCaptureSolver(plateSolver, blindSolver, imagingMediator, filterWheelMediator);

            CaptureSolverParameter parameter = ADP_Tools.CreateCaptureSolverParameter(profileService.ActiveProfile, telescopeMediator.GetCurrentPosition(), SolveAttempts);

            CaptureSequence seq = new CaptureSequence(
                profileService.ActiveProfile.PlateSolveSettings.ExposureTime,
                CaptureSequence.ImageTypes.SNAPSHOT,
                profileService.ActiveProfile.PlateSolveSettings.Filter,
                new BinningMode(profileService.ActiveProfile.PlateSolveSettings.Binning, profileService.ActiveProfile.PlateSolveSettings.Binning),
                1
            );
            return await solver.Solve(seq, parameter, PlateSolveStatusVM.Progress, progress, token);
        }
    public struct CreatiomParams {
        public double MinElevationAboveHorizon;
        public double MinElevation;
        public double MaxElevation;
        public int NumberOfAltitudePoints;
        public int NumberOfAzimuthPoints;
        public int SolveAttempts;
        public int PlateSolveCloseDelay;

    }
}
