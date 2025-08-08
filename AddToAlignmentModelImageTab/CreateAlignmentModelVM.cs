using ADPUK.NINA.AddToAlignmentModel.Locales;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Equipment.Model;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics.Tracing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Navigation;
using static ADPUK.NINA.AddToAlignmentModel.ModelPointCreator;
namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelImageTab {
    [Export(typeof(IDockableVM))]
    [JsonObject(MemberSerialization.OptIn)]


    public partial class CreateAlignmentModelVM : DockableVM, IValidatable {
        private ICameraMediator cameraMediator;
        private ITelescopeMediator telescopeMediator;
        private IRotatorMediator rotatorMediator;
        private IImagingMediator imagingMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IPlateSolverFactory plateSolverFactory;
        private IWindowServiceFactory windowServiceFactory;
        private PluginOptionsAccessor pluginSettings;
        private int _stepCount;
        private bool _isReadOnly;
        private bool _IsPaused;
        private ModelCreationParameters modelCreationParameters;


        public bool IsPaused {
            get {
                return _IsPaused;
            }
            set {
                _IsPaused = value;
                RaisePropertyChanged();
            }

        }

        private ListModelModelPoints _ModelPoints;
        private CancellationTokenSource executeCTS;
        private CancellationTokenSource pauseCTS;

        public IAsyncRelayCommand StartCreate { get; }
        public IAsyncRelayCommand StopCreate { get; }
        public IRelayCommand PauseCreate { get; }
        public IAsyncRelayCommand ResumeCreate { get; }

        public bool CanExecute { get { return Validate(); } }

        public ListModelModelPoints ModelPoints {
            get { return _ModelPoints; }
            set { _ModelPoints = value; }
        }

        public bool IsReadOnly {
            get { return _isReadOnly; }
            set {
                _isReadOnly = value;
                RaisePropertyChanged();
            }
        }

        public int TotalSteps {
            get { return NumberOfAltitudePoints * NumberOfAzimuthPoints; }
        }
        public int StepCount {
            get { return _stepCount; }
            set {
                _stepCount = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int NumberOfAzimuthPoints {
            get {
                modelCreationParameters.NumberOfAzimuthPoints = pluginSettings.GetValueInt32(nameof(NumberOfAzimuthPoints), 4);
                return modelCreationParameters.NumberOfAzimuthPoints;
            }
            set {
                if (value < 3) value = 3;
                if (pluginSettings != null) {
                    pluginSettings?.SetValueInt32(nameof(NumberOfAzimuthPoints), value);
                    modelCreationParameters.NumberOfAzimuthPoints = value;
                    RaisePropertyChanged("TotalSteps");
                    RaisePropertyChanged();
                }
            }
        }
        [JsonProperty]

        public int NumberOfAltitudePoints {
            get {
                modelCreationParameters.NumberOfAltitudePoints = pluginSettings.GetValueInt32(nameof(NumberOfAltitudePoints), 1);
                return modelCreationParameters.NumberOfAltitudePoints;
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings?.SetValueInt32(nameof(NumberOfAltitudePoints), value);
                    modelCreationParameters.NumberOfAltitudePoints = value;
                    RaisePropertyChanged(nameof(TotalSteps));
                    RaisePropertyChanged();
                }
            }
        }
        [JsonProperty]

        public double MaxElevation {
            get {
                modelCreationParameters.MaxElevation = pluginSettings.GetValueDouble(nameof(MaxElevation), 45.0);
                return modelCreationParameters.MaxElevation;
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings?.SetValueDouble(nameof(MaxElevation), value);
                    modelCreationParameters.MaxElevation = value;
                    RaisePropertyChanged();
                }
            }
        }
        [JsonProperty]

        public double MinElevation {
            get {
                modelCreationParameters.MinElevation = pluginSettings.GetValueDouble(nameof(MinElevation), 45.0);
                return modelCreationParameters.MinElevation;
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings?.SetValueDouble(nameof(MinElevation), value);
                    modelCreationParameters.MinElevation = value;
                    RaisePropertyChanged();
                }
            }
        }
        [JsonProperty]
        public int SolveAttempts {
            get {
                modelCreationParameters.SolveAttempts = pluginSettings.GetValueInt32(nameof(SolveAttempts), profileService.ActiveProfile.PlateSolveSettings.NumberOfAttempts);
                return modelCreationParameters.SolveAttempts;
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings.SetValueInt32(nameof(SolveAttempts), value);
                    modelCreationParameters.SolveAttempts = value;
                    RaisePropertyChanged();
                }
            }
        }

        [JsonProperty]
        public int PlateSolveCloseDelay {
            get {
                modelCreationParameters.PlateSolveCloseDelay = pluginSettings.GetValueInt32(nameof(PlateSolveCloseDelay), 5);
                return modelCreationParameters.PlateSolveCloseDelay;
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings.SetValueInt32(nameof(PlateSolveCloseDelay), value);
                    modelCreationParameters.PlateSolveCloseDelay = value;
                    RaisePropertyChanged();
                }
            }
        }

        [JsonProperty]
        public double MinElevationAboveHorizon {
            get {
                modelCreationParameters.MinElevationAboveHorizon = pluginSettings.GetValueDouble(nameof(MinElevationAboveHorizon), 0.0);
                return modelCreationParameters.MinElevationAboveHorizon;
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings.SetValueDouble(nameof(MinElevationAboveHorizon), value);
                    modelCreationParameters.MinElevationAboveHorizon = value;
                    RaisePropertyChanged();
                }

            }
        }

        private bool EnableEquatorialMounts {
            get {
                return pluginSettings.GetValueBoolean(nameof(EnableEquatorialMounts), false);
            }
        }
        public PlateSolvingStatusVM PlateSolveStatusVM { get; } = new PlateSolvingStatusVM();

        public struct CreateParams {
            public double MinElevationAboveHorizon;
            public int PlateSolveDelay;
            public int SolveAttempts;

        }

        [ImportingConstructor]
        public CreateAlignmentModelVM(
                            IProfileService profileServiceforBase,
                            ITelescopeMediator telescopeMediator,
                            IRotatorMediator rotatorMediator,
                            IImagingMediator imagingMediator,
                            IFilterWheelMediator filterWheelMediator,
                            IPlateSolverFactory plateSolverFactory,
                            IWindowServiceFactory windowServiceFactory,
                            ICameraMediator cameraMediator) : base(profileServiceforBase) {
            this.telescopeMediator = telescopeMediator;
            this.rotatorMediator = rotatorMediator;
            this.imagingMediator = imagingMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.plateSolverFactory = plateSolverFactory;
            this.windowServiceFactory = windowServiceFactory;
            this.cameraMediator = cameraMediator;
            ModelPoints = new ListModelModelPoints();
            MaxElevation = 80.0;
            MinElevation = 30.0;
            NumberOfAltitudePoints = 2;
            NumberOfAzimuthPoints = 6;
            Title = "Alignment Model for CPWI";
            executeCTS = new CancellationTokenSource();
            StartCreate = new AsyncRelayCommand(ExecuteCreate);
            PauseCreate = new AsyncRelayCommand(PauseCreation);
            StopCreate = new AsyncRelayCommand(CancelCreation);
            ResumeCreate = new AsyncRelayCommand(ResumeCreation);
            telescopeMediator.Connected += ConnectionChange;
            telescopeMediator.Disconnected += ConnectionChange;
            cameraMediator.Connected += ConnectionChange;
            cameraMediator.Disconnected += ConnectionChange;
            pluginSettings = new PluginOptionsAccessor(profileServiceforBase, Guid.Parse(Assembly.GetExecutingAssembly().GetCustomAttribute<System.Runtime.InteropServices.GuidAttribute>().Value));
            modelCreationParameters = new ModelCreationParameters();
        }

        private Task ConnectionChange(object arg1, EventArgs arg2) {
            RaisePropertyChanged(nameof(CanExecute));
            if (!CanExecute && StartCreate.IsRunning) {
                PauseCreation();
            }
            return Task.CompletedTask;
        }


        private IList<string> issues = [];

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }
        public Task ExecuteCreate() {
            Task createTask = ExecuteCreate(new Progress<ApplicationStatus>(), executeCTS.Token);
            pauseCTS = new CancellationTokenSource();
            return createTask;
        }
        public Task PauseCreation() {
            Task pTask = pauseTask();
            IsPaused = true;
            return pTask;
        }
        public async Task pauseTask() {
            try {
                while (IsPaused) {
                    await Task.Delay(100000, pauseCTS.Token);
                }
            } catch (OperationCanceledException) { }
            return;
        }
        public Task ResumeCreation() {
            pauseCTS.Cancel();
            IsPaused = false;
            return Task.CompletedTask;
        }
        public Task CancelCreation() {
            executeCTS.Cancel();
            return Task.CompletedTask;
        }
        public async Task ExecuteCreate(IProgress<ApplicationStatus> progress, CancellationToken token) {
            StepCount = 0;
            IsReadOnly = true;
            try {
                TelescopeInfo telescopeInfo = telescopeMediator.GetInfo();
                TopocentricCoordinates altAzTarget;
                double initialAzimuth = ADP_Tools.ReadyToStart(telescopeInfo);
                double targetAz = initialAzimuth;
                double nextAz = initialAzimuth;
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
                for (int azc = 1; azc <= NumberOfAzimuthPoints; azc++) {
                    targetAz = nextAz < 360.0 ? nextAz : nextAz - 360.0;
                    for (double nextAlt = MinElevation; nextAlt <= MaxElevation; nextAlt += modelCreationParameters.AltStepSize) {
                        if (IsPaused) { await pauseTask(); }
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

            } catch (OperationCanceledException) {
                Notification.ShowWarning(ViewStrings.ModelBuilderCancelled);
            } finally {
                IsReadOnly = false;
            }

        }
        public virtual bool Validate() {
            Issues = ADP_Tools.ValidateConnections(telescopeMediator.GetInfo(), cameraMediator.GetInfo(), pluginSettings);
            return Issues.Count == 0;
        }

        public override string ToString() {
            return $"Category: Docakables, Item: {nameof(CreateAlignmentModelVM)}";
        }

    }
}