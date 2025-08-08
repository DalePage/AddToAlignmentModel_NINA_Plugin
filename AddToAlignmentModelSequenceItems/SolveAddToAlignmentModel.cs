using ADPUK.NINA.AddToAlignmentModel.Locales;
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
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelSequenceItems {
    [ExportMetadata("Name", "Solve and Add to Alignment Model")]
    [ExportMetadata("Description", "The instruction carries out a plate solve and adds the computed location to the mount's alignment model")]
    [ExportMetadata("Icon", "CrosshairSVG")]
    [ExportMetadata("Category", "Add To CPWI Alignment Model")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SolveAddToAlignmentModel : SequenceItem, IValidatable {
        private IProfileService profileService;
        private ITelescopeMediator telescopeMediator;
        private IRotatorMediator rotatorMediator;
        private IImagingMediator imagingMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IPlateSolverFactory plateSolverFactory;
        private IWindowServiceFactory windowServiceFactory;
        private ICameraMediator cameraMediator;
        private int _maximumAttempts;
        private bool? _displayPlateSolveDetails;
        private int _plateSolveAttempts;
        private int _plateSolveCloseDelay;

        private IPluginOptionsAccessor pluginSettings;

        [JsonProperty]
        public int MaximumAttemptsToCentre {
            get {
                if (_maximumAttempts <= 0) {
                    _maximumAttempts = 1;
                    RaisePropertyChanged();
                }
                return _maximumAttempts;
            }
            set {
                _maximumAttempts = value;
                RaisePropertyChanged();
            }
        }
        [JsonProperty]
        public bool DisplayPlateSolveDetails {
            get {
                if (_displayPlateSolveDetails is null) {
                    _displayPlateSolveDetails = true;
                    RaisePropertyChanged();
                }
                return _displayPlateSolveDetails ?? true;
            }
            set {
                _displayPlateSolveDetails = value;
                RaisePropertyChanged();
            }
        }
        [JsonProperty]
        public int PlaySolveAttempts {
            get {
                if (_plateSolveAttempts <= 0) {
                    _plateSolveAttempts = 0;
                    RaisePropertyChanged();
                }
                return _plateSolveAttempts;
            }
            set {
                _plateSolveAttempts = value;
                RaisePropertyChanged();
            }
        }

        public int PlateSolveCloseDelay {
            get { return _plateSolveCloseDelay; }
            set {
                _plateSolveCloseDelay = value;
                RaisePropertyChanged();
            }
        }

        public PlateSolvingStatusVM PlateSolveStatusVM { get; } = new PlateSolvingStatusVM();

        [ImportingConstructor]
        public SolveAddToAlignmentModel(IProfileService profileService,
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
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(Assembly.GetExecutingAssembly().GetCustomAttribute<System.Runtime.InteropServices.GuidAttribute>().Value));

        }

        private SolveAddToAlignmentModel(SolveAddToAlignmentModel cloneMe) : this(cloneMe.profileService,
                                                          cloneMe.telescopeMediator,
                                                          cloneMe.rotatorMediator,
                                                          cloneMe.imagingMediator,
                                                          cloneMe.filterWheelMediator,
                                                          cloneMe.plateSolverFactory,
                                                          cloneMe.windowServiceFactory,
                                                          cloneMe.cameraMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new SolveAddToAlignmentModel(this);
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
            try {
                progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
                ModelPointCreator modelCreator = new ModelPointCreator(
                    cameraMediator,
                    telescopeMediator,
                    rotatorMediator,
                    imagingMediator,
                    filterWheelMediator,
                    plateSolverFactory,
                    windowServiceFactory,
                    profileService);

                await modelCreator.SolveDirectToMount(MaximumAttemptsToCentre, PlateSolveCloseDelay, progress, token);
            } finally {
            }
        }

        public virtual bool Validate() {
            Issues = ADP_Tools.ValidateConnections(telescopeMediator.GetInfo(), cameraMediator.GetInfo(), pluginSettings);
            return Issues.Count == 0;
        }


        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SolveAddToAlignmentModel)}";
        }
    }

}
