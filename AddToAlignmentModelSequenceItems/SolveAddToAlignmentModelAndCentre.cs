using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelSequenceItems {
    [ExportMetadata("Name", "Solve and Add to Alignment Model and Centre")]
    [ExportMetadata("Description", "The instruction carries out a plate solve and adds the computed location to the mount's alignment model and " +
        "then slews to the original target and repeats until within alignment platesolve tolerance")]
    [ExportMetadata("Icon", "CrosshairSVG")]
    [ExportMetadata("Category", "Add To CPWI Alignment Model")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SolveAddToAlignmentModelAndCentre : SequenceItem, IValidatable {
        private IProfileService profileService;
        private ITelescopeMediator telescopeMediator;
        private IRotatorMediator rotatorMediator;
        private IImagingMediator imagingMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IPlateSolverFactory plateSolverFactory;
        private IWindowServiceFactory windowServiceFactory;
        private ICameraMediator cameraMediator;
        private IDomeMediator domeMediator;
        private IDomeFollower domeFollower;
        private int _maximumAttempts;
        private int _attemptCount;
        public PlateSolvingStatusVM PlateSolveStatusVM { get; } = new PlateSolvingStatusVM();

        [JsonProperty]
        public int MaximumAttemptsToCentre {
            get { return _maximumAttempts; }
            set {
                _maximumAttempts = value;
                RaisePropertyChanged();
            }
        }
        public int AttemptCount {
            get { return (int)_attemptCount; }
            set {
                _attemptCount = value;
                RaisePropertyChanged();
            }
        }

        [ImportingConstructor]
        public SolveAddToAlignmentModelAndCentre(IProfileService profileService,
                            ITelescopeMediator telescopeMediator,
                            IRotatorMediator rotatorMediator,
                            IImagingMediator imagingMediator,
                            IFilterWheelMediator filterWheelMediator,
                            IPlateSolverFactory plateSolverFactory,
                            IWindowServiceFactory windowServiceFactory,
                            ICameraMediator cameraMediator,
                            IDomeMediator domeMediator,
                            IDomeFollower domeFollower) {
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.rotatorMediator = rotatorMediator;
            this.imagingMediator = imagingMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.cameraMediator = cameraMediator;
            this.domeMediator = domeMediator;
            this.domeFollower = domeFollower;
            MaximumAttemptsToCentre = 4;
        }

        private SolveAddToAlignmentModelAndCentre(SolveAddToAlignmentModelAndCentre cloneMe) : this(cloneMe.profileService,
                                                          cloneMe.telescopeMediator,
                                                          cloneMe.rotatorMediator,
                                                          cloneMe.imagingMediator,
                                                          cloneMe.filterWheelMediator,
                                                          cloneMe.plateSolverFactory,
                                                          cloneMe.windowServiceFactory,
                                                          cloneMe.cameraMediator,
                                                          cloneMe.domeMediator,
                                                          cloneMe.domeFollower
                                                          ) {
            MaximumAttemptsToCentre = cloneMe.MaximumAttemptsToCentre;
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new SolveAddToAlignmentModelAndCentre(this);
        }

        private IList<string> issues = [];

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            AttemptCount = 0;
            bool centred = true;
            IWindowService service = windowServiceFactory.Create();
            Coordinates currentCoordinates = telescopeMediator.GetInfo().Coordinates;
            progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
            service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
            try {
                bool isAboveHorizon;
                do {
                    AttemptCount++;
                    isAboveHorizon = ADP_Tools.AboveHorizon(telescopeMediator.GetCurrentPosition(),
                            profileService.ActiveProfile.AstrometrySettings.Horizon,
                            profileService.ActiveProfile.AstrometrySettings.Latitude);
                    if (isAboveHorizon) {
                        PlateSolveResult result = await DoSolve(progress, token);
                        if (!result.Success) {
                            throw new SequenceEntityFailedException(Loc.Instance["LblPlatesolveFailed"]);
                        } else {
                            var resultCoordinates = result.Coordinates.Transform(Epoch.JNOW);
                            string addAlignmentResponse = telescopeMediator.Action("Telescope:AddAlignmentReference", $"{resultCoordinates.RA}:{resultCoordinates.Dec}");
                            if (Math.Abs(result.Separation.Distance.ArcSeconds) > profileService.ActiveProfile.PlateSolveSettings.Threshold) {
                                centred = false;
                                await telescopeMediator.SlewToCoordinatesAsync(currentCoordinates, token);
                            }
                        }
                    } else {
                        Notification.ShowWarning("Target is below the horizon");
                    }
                } while (!centred && AttemptCount <= MaximumAttemptsToCentre && isAboveHorizon);
            } finally {
                service.DelayedClose(TimeSpan.FromSeconds(10));
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
            if (!Regex.IsMatch(scopeInfo.Name ?? "", "CPWI", RegexOptions.IgnoreCase)) {
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
            return $"Category: {Category}, Item: {nameof(SolveAddToAlignmentModel)}";
        }
    }

}
