using ADPUK.NINA.AddToAlignmentModel.Locales;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
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
using NINA.Sequencer.Validations;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelImageTab {
    [Export(typeof(IDockableVM))]
    [JsonObject(MemberSerialization.OptIn)]


    public class CreateAlignmentModelVM : DockableVM, IValidatable {
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

        public bool IsPaused {
            get {
                return _IsPaused;
            }
            set {
                _IsPaused = value;
                RaisePropertyChanged();
            }

        }

        private ObservableCollection<ModelPoint> _ModelPoints;
        private CancellationTokenSource executeCTS;
        private CancellationTokenSource pauseCTS;

        public IAsyncRelayCommand StartCreate { get; }
        public IAsyncRelayCommand StopCreate { get; }
        public IRelayCommand PauseCreate { get; }
        public IAsyncRelayCommand ResumeCreate { get; }

        public bool CanExecute { get { return Validate(); } }
        public ObservableCollection<ModelPoint> ModelPoints {
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
            get { return pluginSettings.GetValueInt32(nameof(NumberOfAzimuthPoints), 4); }
            set {
                if (value < 3) value = 3;
                if (pluginSettings != null) {
                    pluginSettings?.SetValueInt32(nameof(NumberOfAzimuthPoints), value);
                    RaisePropertyChanged("TotalSteps");
                    RaisePropertyChanged();
                }
            }
        }
        [JsonProperty]

        public int NumberOfAltitudePoints {
            get {
                return pluginSettings.GetValueInt32(nameof(NumberOfAltitudePoints), 1);
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings?.SetValueInt32(nameof(NumberOfAltitudePoints), value);
                    RaisePropertyChanged(nameof(TotalSteps));
                    RaisePropertyChanged();
                }
            }
        }
        [JsonProperty]

        public double MaxElevation {
            get { return pluginSettings.GetValueDouble(nameof(MaxElevation), 45.0); }
            set {
                if (pluginSettings != null) {
                    pluginSettings?.SetValueDouble(nameof(MaxElevation), value);
                    RaisePropertyChanged();
                }
            }
        }
        [JsonProperty]

        public double MinElevation {
            get { return pluginSettings.GetValueDouble(nameof(MinElevation), 45.0); }
            set {
                if (pluginSettings != null) {
                    pluginSettings?.SetValueDouble(nameof(MinElevation), value);
                    RaisePropertyChanged();
                }
            }
        }
        [JsonProperty]
        public int SolveAttempts {
            get {
                return pluginSettings.GetValueInt32(nameof(SolveAttempts), profileService.ActiveProfile.PlateSolveSettings.NumberOfAttempts);
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings.SetValueInt32(nameof(SolveAttempts), value);
                    RaisePropertyChanged();
                }
            }
        }

        public double MinElevationAboveHorizon {
            get {
                return pluginSettings.GetValueDouble(nameof(MinElevationAboveHorizon), 0.0);
            }
            set {
                if (pluginSettings != null) {
                    pluginSettings.SetValueDouble(nameof(MinElevationAboveHorizon), value);
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
            MaxElevation = 80.0;
            MinElevation = 30.0;
            NumberOfAltitudePoints = 2;
            NumberOfAzimuthPoints = 6;
            Title = "Alignment Model for CPWI";
            ModelPoints = new ObservableCollection<ModelPoint>();
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
            IWindowService service = windowServiceFactory.Create();
            progress = PlateSolveStatusVM.CreateLinkedProgress(progress);
            try {
                double altStep = 0;
                if (Math.Abs(MaxElevation - MinElevation) < 5) NumberOfAltitudePoints = 1;
                if (NumberOfAltitudePoints > 1) {
                    altStep = ((MaxElevation - MinElevation) / (NumberOfAltitudePoints - 1));
                } else {
                    MinElevation = ((MinElevation + MaxElevation) / 2.0);
                    altStep = (MaxElevation + MinElevation) / 2.0;
                }
                double azStep = (360.0 / NumberOfAzimuthPoints);
                TelescopeInfo telescopeInfo = telescopeMediator.GetInfo();
                TopocentricCoordinates altAzTarget;
                double initialAzimuth = ADP_Tools.ReadyToStart(telescopeInfo);
                double targetAz = initialAzimuth;
                StepCount = 0;
                for (double nextAz = initialAzimuth; nextAz < initialAzimuth + 360.0 + (0.1 * azStep); nextAz += azStep) {
                    targetAz = nextAz < 360.0 ? nextAz : nextAz - 360.0;
                    for (double nextAlt = MinElevation; nextAlt <= MaxElevation; nextAlt += altStep) {
                        if (IsPaused) { await pauseTask(); }
                        service.DelayedClose(new TimeSpan(0, 0, 10));
                        altAzTarget = new TopocentricCoordinates(
                            Angle.ByDegree(targetAz),
                            Angle.ByDegree(nextAlt),
                            Angle.ByDegree(telescopeInfo.SiteLatitude),
                            Angle.ByDegree(telescopeInfo.SiteLongitude)
                            );
                        if (ADP_Tools.AboveMinAlt(
                                altAzTarget,
                                profileService.ActiveProfile.AstrometrySettings.Horizon,
                                MinElevationAboveHorizon)) {

                            await telescopeMediator.SlewToCoordinatesAsync(altAzTarget.Transform(Epoch.JNOW), token);
                            service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
                            if (cameraMediator.GetInfo().Connected) {
                                if (IsPaused) { await pauseTask(); }
                                PlateSolveResult result = await DoSolve(progress, token);
                                if (!result.Success) {
                                    Notification.ShowWarning($"{ViewStrings.PlateSolveFailedAt.Replace("{{Azimuth}}", targetAz.ToString()).Replace("{{Altitude}}}", targetAz.ToString())}");
                                } else {
                                    Coordinates resultCoordinates = result.Coordinates.Transform(Epoch.JNOW);
                                    string addAlignmentResponse = telescopeMediator.Action("Telescope:AddAlignmentReference", $"{resultCoordinates.RA}:{resultCoordinates.Dec}");
                                    ModelPoint modelPoint = new ModelPoint(altAzTarget, result);
                                    ModelPoints.Add(modelPoint);
                                }
                            } else {
                                ModelPoint failedModelPoint = new ModelPoint();
                                failedModelPoint.TargetAlt = altAzTarget.Altitude.Degree;
                                failedModelPoint.TargetAz = altAzTarget.Azimuth.Degree;
                                ModelPoints.Add(failedModelPoint);
                                Notification.ShowWarning(Loc.Instance["Lbl_CameraNotConnected"]);
                            }
                        } else {
                            Notification.ShowWarning($"{ViewStrings.TargetBelowHorizon.Replace("{{Azimuth}}", targetAz.ToString()).Replace("{{Altitude}}", nextAlt.ToString())}");
                        }
                        StepCount++;
                    }
                }

            } catch (OperationCanceledException) {
                Notification.ShowWarning(ViewStrings.ModelBuilderCancelled);
            } finally {
                service.DelayedClose(new TimeSpan(0, 0, 10));
                IsReadOnly = false;
            }

        }

        protected virtual async Task<PlateSolveResult> DoSolve(IProgress<ApplicationStatus> progress, CancellationToken token) {
            var plateSolver = plateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);
            var blindSolver = plateSolverFactory.GetBlindSolver(profileService.ActiveProfile.PlateSolveSettings);

            var solver = plateSolverFactory.GetCaptureSolver(plateSolver, blindSolver, imagingMediator, filterWheelMediator);

            var parameter = ADP_Tools.CreateCaptureSolverParameter(profileService.ActiveProfile, telescopeMediator.GetCurrentPosition(), SolveAttempts);

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
            Issues = ADP_Tools.ValidateConnections(telescopeMediator.GetInfo(), cameraMediator.GetInfo(), pluginSettings);
            return Issues.Count == 0;
        }

        public override string ToString() {
            return $"Category: Docakables, Item: {nameof(CreateAlignmentModelVM)}";
        }
    }

}
