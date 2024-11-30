using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model.Equipment;
using NINA.Core.Model;
using NINA.Core.Utility.WindowService;
using NINA.Core.Enum;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.PlateSolving.Interfaces;
using NINA.PlateSolving;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using NINA.Astrometry;
using NINA.Core.Utility.Notification;
using System.Windows;
using NINA.Equipment.Equipment.MyTelescope;
using System.Text.Json;
using Namotion.Reflection;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelSequenceItems {
    [ExportMetadata("Name", "Create Alignement Model")]
    [ExportMetadata("Description", "The instruction carries plate solves at a number of AltAz points and adds them to the pointing model.")]
    [ExportMetadata("Icon", "CrosshairSVG")]
    [ExportMetadata("Category", "Add To CPWI Alignment Model")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class CreateAlignmentModel : SequenceItem, IValidatable {
        private ICameraMediator cameraMediator;
        private IProfileService profileService;
        private ITelescopeMediator telescopeMediator;
        private IRotatorMediator rotatorMediator;
        private IImagingMediator imagingMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IPlateSolverFactory plateSolverFactory;
        private IWindowServiceFactory windowServiceFactory;
        private int _numberOfAzimuthPoints;
        private int _numberOfAltitudePoints;
        private double _maxElevation;
        private double _minElevation;
        private int _stepCount;
        private bool _isReadOnly;

        public bool isReadOnly {
            get { return _isReadOnly; }
            set {
                _isReadOnly = value;
                RaisePropertyChanged();
            }
        }

        public int total_Steps {
            get { return _numberOfAltitudePoints * _numberOfAzimuthPoints; }
        }
        public int stepCount {
            get { return _stepCount; }
            set {
                _stepCount = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int numberOfAzimuthPoints {
            get { return _numberOfAzimuthPoints; }
            set {
                _numberOfAzimuthPoints = value;
                RaisePropertyChanged("total_Steps");
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int numberOfAltitudePoints {
            get { return _numberOfAltitudePoints; }
            set {
                _numberOfAltitudePoints = value;
                RaisePropertyChanged("total_Steps");
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public double maxElevation {
            get { return _maxElevation; }
            set {
                _maxElevation = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public double minElevation {
            get { return _minElevation; }
            set {
                _minElevation = value;
                RaisePropertyChanged();
            }
        }
        public PlateSolvingStatusVM PlateSolveStatusVM { get; } = new PlateSolvingStatusVM();

        [ImportingConstructor]
        public CreateAlignmentModel(IProfileService profileService,
                            ITelescopeMediator telescopeMediator,
                            IRotatorMediator rotatorMediator,
                            IImagingMediator imagingMediator,
                            IFilterWheelMediator filterWheelMediator,
                            IPlateSolverFactory plateSolverFactory,
                            IWindowServiceFactory windowServiceFactory,
                            ICameraMediator cameraMediator) {
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.rotatorMediator = rotatorMediator;
            this.imagingMediator = imagingMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.cameraMediator = cameraMediator;
            maxElevation = 80.0;
            minElevation = 30.0;
            numberOfAltitudePoints = 2;
            numberOfAzimuthPoints = 6;
        }

        private CreateAlignmentModel(CreateAlignmentModel cloneMe) : this(cloneMe.profileService,
                                                          cloneMe.telescopeMediator,
                                                          cloneMe.rotatorMediator,
                                                          cloneMe.imagingMediator,
                                                          cloneMe.filterWheelMediator,
                                                          cloneMe.plateSolverFactory,
                                                          cloneMe.windowServiceFactory,
                                                          cloneMe.cameraMediator) {
            CopyMetaData(cloneMe);
            maxElevation = cloneMe.maxElevation;
            minElevation = cloneMe.minElevation;
            numberOfAzimuthPoints = cloneMe.numberOfAzimuthPoints;
            numberOfAltitudePoints = cloneMe.numberOfAltitudePoints;
        }

        public override object Clone() {
            return new CreateAlignmentModel(this);
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            stepCount = 0;
            isReadOnly = true;
            IWindowService service = windowServiceFactory.Create();
            progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
            try {
                double altStep = 0;
                if (numberOfAltitudePoints > 1) {
                    altStep = (maxElevation - minElevation) / (numberOfAltitudePoints - 1);
                } else {
                    minElevation = (minElevation + maxElevation) / 2.0;
                    altStep = (maxElevation + minElevation / 2.0);
                }
                double azStep = 360.0 / numberOfAzimuthPoints;
                double initialAzimuth = 0.0;
                double targetAz = initialAzimuth;
                string hemisphere = "north";
                TelescopeInfo telescopeInfo = telescopeMediator.GetInfo();
                TopocentricCoordinates altAzTarget = null;
                if (telescopeMediator.GetInfo().SiteLatitude < 0.0) {
                    hemisphere = "south";
                    initialAzimuth = 180.0;
                }
                MessageBoxResult boxResult = MessageBox.Show($"Please ensure the scope is roughly pointing at the horizon due {hemisphere}", "Pre-Alignment", MessageBoxButton.OKCancel);
                if (boxResult != MessageBoxResult.OK) {
                    throw new SequenceEntityFailedException($"Scope pe-alignemt to {hemisphere}ern horizon not confirmed");
                }
                if (Math.Abs(telescopeMediator.GetInfo().Azimuth - initialAzimuth) > 10.0 || Math.Abs(telescopeMediator.GetInfo().Altitude) > 10.0) {
                    throw new SequenceEntityFailedException($"Scope does not appear to be pointing at the {hemisphere}ern horizon");
                }
                for (double nextAz = initialAzimuth; nextAz < initialAzimuth + 360 + (0.1 * azStep); nextAz += azStep) {
                    targetAz = nextAz < 360.0 ? nextAz : nextAz - 360.0;

                    for (double nextAlt = minElevation; nextAlt <= maxElevation; nextAlt += altStep) {
                        stepCount++;
                        altAzTarget = new TopocentricCoordinates(
                            Angle.ByDegree(targetAz),
                            Angle.ByDegree(nextAlt),
                            Angle.ByDegree(telescopeInfo.SiteLatitude),
                            Angle.ByDegree(telescopeInfo.SiteLongitude)
                            );
                        if (ADP_Tools.AboveHorizon(
                                altAzTarget,
                                profileService.ActiveProfile.AstrometrySettings.Horizon)) {

                            Task[] taskList = [telescopeMediator.SlewToCoordinatesAsync(altAzTarget, token), service.Close() ];
                            Task.WaitAll(taskList, token);
                            if (token.IsCancellationRequested) { return; }
                            service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
                            PlateSolveResult result = await DoSolve(progress, token);
                            if (!result.Success) {
                                Notification.ShowWarning($"Plate solve faild at Az: {targetAz}, Alt: {nextAlt}");
                            } else {
                                string addAlignmentResponse = telescopeMediator.Action("Telescope:AddAlignmentReference", $"{result.Coordinates.RA}:{result.Coordinates.Dec}");
                            }
                        } else {
                            Notification.ShowWarning($"Target at Az: {targetAz}, Alt: {nextAlt} is below the horizon");
                        }
                    }
                }
            } finally {
                service.DelayedClose(new TimeSpan(0, 0, 10));
                isReadOnly = false;
            }

        }

        protected virtual async Task<PlateSolveResult> DoSolve(IProgress<ApplicationStatus> progress, CancellationToken token) {
            var plateSolver = plateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);
            var blindSolver = plateSolverFactory.GetBlindSolver(profileService.ActiveProfile.PlateSolveSettings);

            var solver = plateSolverFactory.GetCaptureSolver(plateSolver, blindSolver, imagingMediator, filterWheelMediator);
            var parameter = new CaptureSolverParameter() {
                Attempts = profileService.ActiveProfile.PlateSolveSettings.NumberOfAttempts,
                Binning = profileService.ActiveProfile.PlateSolveSettings.Binning,
                Coordinates = telescopeMediator.GetCurrentPosition(),
                DownSampleFactor = profileService.ActiveProfile.PlateSolveSettings.DownSampleFactor,
                FocalLength = profileService.ActiveProfile.TelescopeSettings.FocalLength,
                MaxObjects = profileService.ActiveProfile.PlateSolveSettings.MaxObjects,
                PixelSize = profileService.ActiveProfile.CameraSettings.PixelSize,
                ReattemptDelay = TimeSpan.FromMinutes(profileService.ActiveProfile.PlateSolveSettings.ReattemptDelay),
                Regions = profileService.ActiveProfile.PlateSolveSettings.Regions,
                SearchRadius = profileService.ActiveProfile.PlateSolveSettings.SearchRadius,
                BlindFailoverEnabled = profileService.ActiveProfile.PlateSolveSettings.BlindFailoverEnabled
            };

            var seq = new CaptureSequence(
                profileService.ActiveProfile.PlateSolveSettings.ExposureTime,
                CaptureSequence.ImageTypes.SNAPSHOT,
                profileService.ActiveProfile.PlateSolveSettings.Filter,
                new BinningMode(profileService.ActiveProfile.PlateSolveSettings.Binning, profileService.ActiveProfile.PlateSolveSettings.Binning),
                1
            );
            return await solver.Solve(seq, parameter, PlateSolveStatusVM.Progress, progress, token);
        }

        public virtual bool Validate() {
            var i = new List<string>();
            var scopeInfo = telescopeMediator.GetInfo();
            if (!scopeInfo.Connected) {
                i.Add(Loc.Instance["LblTelescopeNotConnected"]);
            }
            if (scopeInfo.Name != "CPWI") {
                i.Add("Only works with CPWI scopes");
            }
            if (scopeInfo.AlignmentMode != AlignmentMode.AltAz) {
                i.Add("Only works with AltAz mounts");
            }
            if (!cameraMediator.GetInfo().Connected) {
                i.Add("Camera not connected");
            }

            Issues = i;
            return i.Count == 0;
        }


        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(CreateAlignmentModel)}";
        }
    }

}
