using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model.Equipment;
using NINA.Core.Model;
using NINA.Core.Utility.WindowService;
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

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelSequenceItems
{
    [ExportMetadata("Name", "Solve Sync and Add to Alignment Model")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Platesolving_SolveAndSync_Description")]
    [ExportMetadata("Icon", "CrosshairSVG")]
    [ExportMetadata("Category", "Add To Alignment Model")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SolveSyncAddToAlignmentModel : SequenceItem, IValidatable {
        private IProfileService profileService;
        private ITelescopeMediator telescopeMediator;
        private IRotatorMediator rotatorMediator;
        private IImagingMediator imagingMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IPlateSolverFactory plateSolverFactory;
        private IWindowServiceFactory windowServiceFactory;
        public PlateSolvingStatusVM PlateSolveStatusVM { get; } = new PlateSolvingStatusVM();

        [ImportingConstructor]
        public SolveSyncAddToAlignmentModel(IProfileService profileService,
                            ITelescopeMediator telescopeMediator,
                            IRotatorMediator rotatorMediator,
                            IImagingMediator imagingMediator,
                            IFilterWheelMediator filterWheelMediator,
                            IPlateSolverFactory plateSolverFactory,
                            IWindowServiceFactory windowServiceFactory) {
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.rotatorMediator = rotatorMediator;
            this.imagingMediator = imagingMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
        }

        private SolveSyncAddToAlignmentModel(SolveSyncAddToAlignmentModel cloneMe) : this(cloneMe.profileService,
                                                          cloneMe.telescopeMediator,
                                                          cloneMe.rotatorMediator,
                                                          cloneMe.imagingMediator,
                                                          cloneMe.filterWheelMediator,
                                                          cloneMe.plateSolverFactory,
                                                          cloneMe.windowServiceFactory) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new SolveSyncAddToAlignmentModel(this);
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
            var service = windowServiceFactory.Create();
            progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
            service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
            try {
                var result = await DoSolve(progress, token);
                if (result.Success == false) {
                    throw new SequenceEntityFailedException(Loc.Instance["LblPlatesolveFailed"]);
                } else {
                    var sync = await telescopeMediator.Sync(result.Coordinates);
                    var orientation = (float)result.PositionAngle;
                    rotatorMediator.Sync(orientation);
                    if (!sync) {
                        throw new SequenceEntityFailedException(Loc.Instance["LblSyncFailed"]);
                    }
                    string addAlignmentResponse = telescopeMediator.Action("AddAlignmentReference", $"{result.Coordinates.RADegrees}:{result.Coordinates.Dec}");
                }
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
            if (!telescopeMediator.GetInfo().Connected) {
                i.Add(Loc.Instance["LblTelescopeNotConnected"]);
            }
            Issues = i;
            return i.Count == 0;
        }


        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SolveSyncAddToAlignmentModel)}";
        }
    }

}
