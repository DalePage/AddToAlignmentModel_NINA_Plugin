using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
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
    [ExportMetadata("Name", "Solve and Add to Alignment Model and Centre")]
    [ExportMetadata("Description", "The instruction carries out a plate solve and adds the computed location to the mount's alignment model and " +
        "then slews to the original target and repeats until within the alignment platesolve tolerance")]
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
        private bool _displayPlateSolveDetails;
        private int _plateSolveAttempts;
        public PlateSolvingStatusVM PlateSolveStatusVM { get; } = new PlateSolvingStatusVM();
        private PluginOptionsAccessor pluginSettings;
        private int _plateSolveCloseDelay;

        [JsonProperty]
        public int PlateCloseSolveDelay {
            get { return _plateSolveCloseDelay; }
            set {
                _plateSolveCloseDelay = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int MaximumAttemptsToCentre {
            get { return _maximumAttempts; }
            set {
                _maximumAttempts = value;
                RaisePropertyChanged();
            }
        }
        public int AttemptCount {
            get { return _attemptCount; }
            set {
                _attemptCount = value;
                RaisePropertyChanged();
            }
        }

        public bool DisplayPlateSolveDetails {
            get {
                return _displayPlateSolveDetails;
            }
            set {
                _displayPlateSolveDetails = value;
                RaisePropertyChanged();
            }
        }
        [JsonProperty]
        public int PlateSolveAttempts {
            get {
                return _plateSolveAttempts;
            }
            set {
                _plateSolveAttempts = value;
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
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(Assembly.GetExecutingAssembly().GetCustomAttribute<System.Runtime.InteropServices.GuidAttribute>().Value));
            MaximumAttemptsToCentre = 4;
            DisplayPlateSolveDetails = true;
            PlateSolveAttempts = 5;
            PlateCloseSolveDelay = 5;
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
            DisplayPlateSolveDetails = cloneMe.DisplayPlateSolveDetails;
            PlateSolveAttempts = cloneMe.PlateSolveAttempts;
            PlateCloseSolveDelay = cloneMe.PlateCloseSolveDelay;
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
            bool centred = false;
            Coordinates currentCoordinates = telescopeMediator.GetInfo().Coordinates;
            ModelPointCreator modelCreator = new ModelPointCreator(
                cameraMediator,
                telescopeMediator,
                rotatorMediator,
                imagingMediator,
                filterWheelMediator,
                plateSolverFactory,
                windowServiceFactory,
                profileService);

            bool isAboveHorizon;
            AttemptCount++;
            PlateSolveResult result = null;
            Coordinates currentPosition = telescopeMediator.GetCurrentPosition();
            isAboveHorizon = ADP_Tools.AboveMinAlt(currentPosition,
                    profileService.ActiveProfile.AstrometrySettings.Horizon,
                    profileService.ActiveProfile.AstrometrySettings.Latitude,
                    pluginSettings.GetValueDouble(nameof(AddToAlignmentModel.MinElevationAboveHorizon), 5.0));
            if (isAboveHorizon) {
                result = await modelCreator.SolveDirectToMount(currentPosition, PlateSolveAttempts, PlateCloseSolveDelay, progress, token);
                if (result.Success) {
                    centred = ((currentCoordinates - result.Coordinates).Distance.Degree < profileService.ActiveProfile.PlateSolveSettings.Threshold);
                    while (!centred && AttemptCount <= MaximumAttemptsToCentre && isAboveHorizon) {
                        await telescopeMediator.Sync(result.Coordinates);
                        await telescopeMediator.SlewToCoordinatesAsync(currentCoordinates, token);
                        result = await modelCreator.DoSolve(currentCoordinates, progress, MaximumAttemptsToCentre, token);
                        centred = ((currentCoordinates - result.Coordinates).Distance.Degree < profileService.ActiveProfile.PlateSolveSettings.Threshold);
                    }
                }
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
