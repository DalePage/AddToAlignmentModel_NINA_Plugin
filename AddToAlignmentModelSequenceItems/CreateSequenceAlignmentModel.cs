using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.PlateSolving.Interfaces;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelSequenceItems {
    [ExportMetadata("Name", "Create Alignement Model")]
    [ExportMetadata("Description", "The instruction carries out plate solves at a number of AltAz points and adds them to the pointing model.")]
    [ExportMetadata("Icon", "CrosshairSVG")]
    [ExportMetadata("Category", "Add To CPWI Alignment Model")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class CreateSequenceAlignmentModel : SequenceItem, IValidatable {
        private IPluginManifest pluginManifest;
        private ICameraMediator cameraMediator;
        private IProfileService profileService;
        private ITelescopeMediator telescopeMediator;
        private IRotatorMediator rotatorMediator;
        private IImagingMediator imagingMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IPlateSolverFactory plateSolverFactory;
        private IWindowServiceFactory windowServiceFactory;
        private PluginOptionsAccessor pluginSettings;
        private int _numberOfAzimuthPoints;
        private int _numberOfAltitudePoints;
        private double _maxElevation;
        private double _minElevation;
        private int _stepCount;
        private bool _isReadOnly;
        private int _solveAttempts;
        private ModelPointCreator.ModelCreationParameters modelCreationParameters;
        private bool _displayPlateSolveDetails;
        private int _plateSolveCloseDelay;
        public ObservableCollection<ModelPoint> ModelPoints { get; } = new ObservableCollection<ModelPoint>();

        public bool IsReadOnly {
            get { return _isReadOnly; }
            set {
                _isReadOnly = value;
                RaisePropertyChanged();
            }
        }

        public int TotalSteps {
            get { return _numberOfAltitudePoints * _numberOfAzimuthPoints; }
        }
        public int StepCount {
            get { return _stepCount; }
            set {
                _stepCount = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int PlateSolveCloseDelay {
            get { return _plateSolveCloseDelay; }
            set { _plateSolveCloseDelay = value; RaisePropertyChanged(); }
        }

        [JsonProperty]
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
        public int NumberOfAzimuthPoints {
            get { return _numberOfAzimuthPoints; }
            set {
                _numberOfAzimuthPoints = value;
                RaisePropertyChanged("total_Steps");
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int NumberOfAltitudePoints {
            get { return _numberOfAltitudePoints; }
            set {
                _numberOfAltitudePoints = value;
                RaisePropertyChanged(nameof(TotalSteps));
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public double MaxElevation {
            get { return _maxElevation; }
            set {
                _maxElevation = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public double MinElevation {
            get { return _minElevation; }
            set {
                _minElevation = value;
                RaisePropertyChanged();
            }
        }
        [JsonProperty]
        public int SolveAttempts {
            get {
                if (_solveAttempts > 0) return _solveAttempts;
                return profileService.ActiveProfile.PlateSolveSettings.NumberOfAttempts;
            }
            set {
                _solveAttempts = value;
                RaisePropertyChanged();
            }
        }
        public PlateSolvingStatusVM PlateSolveStatusVM;

        [ImportingConstructor]
        public CreateSequenceAlignmentModel(IProfileService profileService,
                            ITelescopeMediator telescopeMediator,
                            IRotatorMediator rotatorMediator,
                            IImagingMediator imagingMediator,
                            IFilterWheelMediator filterWheelMediator,
                            IPlateSolverFactory plateSolverFactory,
                            IWindowServiceFactory windowServiceFactory,
                            ICameraMediator cameraMediator,
                            IPluginManifest pluginManifest) {
            this.pluginManifest = pluginManifest;
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
            this.rotatorMediator = rotatorMediator;
            this.imagingMediator = imagingMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.cameraMediator = cameraMediator;
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(Assembly.GetExecutingAssembly().GetCustomAttribute<System.Runtime.InteropServices.GuidAttribute>().Value));
            MaxElevation = 80.0;
            MinElevation = 30.0;
            NumberOfAltitudePoints = 2;
            NumberOfAzimuthPoints = 6;
            modelCreationParameters = new ModelPointCreator.ModelCreationParameters();
            PlateSolveStatusVM = new PlateSolvingStatusVM();
            DisplayPlateSolveDetails = true;
            PlateSolveCloseDelay = 5;
        }

        private CreateSequenceAlignmentModel(CreateSequenceAlignmentModel cloneMe) : this(cloneMe.profileService,
                                                          cloneMe.telescopeMediator,
                                                          cloneMe.rotatorMediator,
                                                          cloneMe.imagingMediator,
                                                          cloneMe.filterWheelMediator,
                                                          cloneMe.plateSolverFactory,
                                                          cloneMe.windowServiceFactory,
                                                          cloneMe.cameraMediator,
                                                          cloneMe.pluginManifest) {
            CopyMetaData(cloneMe);
            MaxElevation = cloneMe.MaxElevation;
            MinElevation = cloneMe.MinElevation;
            NumberOfAzimuthPoints = cloneMe.NumberOfAzimuthPoints;
            NumberOfAltitudePoints = cloneMe.NumberOfAltitudePoints;
            modelCreationParameters = new ModelPointCreator.ModelCreationParameters();
            PlateSolveStatusVM = new PlateSolvingStatusVM();
            pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(Assembly.GetExecutingAssembly().GetCustomAttribute<System.Runtime.InteropServices.GuidAttribute>().Value));
            DisplayPlateSolveDetails = true;
            PlateSolveCloseDelay = 5;
        }

        public override object Clone() {
            return new CreateSequenceAlignmentModel(this);
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
            TelescopeInfo telescopeInfo = telescopeMediator.GetInfo();
            TopocentricCoordinates altAzTarget;
            double initialAzimuth = ADP_Tools.ReadyToStart(telescopeInfo);
            double targetAz = initialAzimuth;
            double nextAz = initialAzimuth;
            StepCount = 0;
            IsReadOnly = true;
            try {
                StepCount = 0;
                ModelPointCreator modelCreator = new ModelPointCreator(
                    cameraMediator,
                    telescopeMediator,
                    rotatorMediator,
                    imagingMediator,
                    filterWheelMediator,
                    plateSolverFactory,
                    windowServiceFactory,
                    profileService);

                modelCreationParameters = new ModelPointCreator.ModelCreationParameters {
                    NumberOfAzimuthPoints = NumberOfAzimuthPoints,
                    NumberOfAltitudePoints = NumberOfAltitudePoints,
                    MaxElevation = MaxElevation,
                    MinElevation = MinElevation,
                    SolveAttempts = SolveAttempts,
                    PlateSolveCloseDelay = PlateSolveCloseDelay
                };
                for (int azc = 1; azc <= NumberOfAzimuthPoints; azc++) {
                    targetAz = nextAz < 360.0 ? nextAz : nextAz - 360.0;
                    for (double nextAlt = MinElevation; nextAlt <= MaxElevation; nextAlt += modelCreationParameters.AltStepSize) {
                        altAzTarget = new TopocentricCoordinates(
                            Angle.ByDegree(targetAz),
                            Angle.ByDegree(nextAlt),
                            Angle.ByDegree(telescopeInfo.SiteLatitude),
                            Angle.ByDegree(telescopeInfo.SiteLongitude)
                            );
                        modelCreationParameters.TargetCoordinatesAltAz = altAzTarget;
                        ModelPoint modelPoint = await modelCreator.CreateModelPoint(modelCreationParameters, progress, token);
                        ModelPoints.Add(modelPoint);
                        StepCount++;
                    }
                    nextAz += modelCreationParameters.AzStepSize;
                }
            } finally {
                IsReadOnly = false;
            }

        }

        public virtual bool Validate() {
            Issues = ADP_Tools.ValidateConnections(telescopeMediator.GetInfo(), cameraMediator.GetInfo(), pluginSettings);
            return Issues.Count == 0;
        }


        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(CreateSequenceAlignmentModel)}";
        }
    }

}
