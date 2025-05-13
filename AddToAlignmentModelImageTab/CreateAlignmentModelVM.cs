using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core;
using NinaCoreUtil = NINA.Core.Utility;
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
using Nito.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Media.TextFormatting;
using System.Diagnostics.Eventing.Reader;
using NINA.Core.Utility;
using System.Linq.Expressions;
using System.Reflection;

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

        private ObservableCollection<Coordinates> _ModelPoints;
        private CancellationTokenSource executeCTS;
        private CancellationTokenSource pauseCTS;

        public IAsyncRelayCommand StartCreate { get; }
        public IAsyncRelayCommand StopCreate { get; }
        public IRelayCommand PauseCreate { get; }
        public IAsyncRelayCommand ResumeCreate { get; }

        public bool CanExecute { get { return Validate(); } }
        public ObservableCollection<Coordinates> ModelPoints {
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
            ModelPoints = new ObservableCollection<Coordinates>();
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
                double initialAzimuth = 0.0;
                double targetAz = initialAzimuth;
                string hemisphere = "north";
                TelescopeInfo telescopeInfo = telescopeMediator.GetInfo();
                TopocentricCoordinates altAzTarget = null;
                if (telescopeMediator.GetInfo().SiteLatitude < 0.0) {
                    hemisphere = "south";
                    initialAzimuth = 180.0;
                }
                string checkStartPoint = $"Please ensure the scope is roughly pointing at the horizon due {hemisphere}";
                string checkStartPointHeader = "Pre-Alignment";
                if (Math.Abs(telescopeMediator.GetInfo().Azimuth - initialAzimuth) > 10.0 || Math.Abs(telescopeMediator.GetInfo().Altitude) > 10.0) {
                    checkStartPoint = $"Scope thinks it is pointing to Az: {telescopeMediator.GetInfo().Azimuth}, Alt: {telescopeMediator.GetInfo().Altitude}. \nPlease confirm this is approximately correct!";
                    checkStartPointHeader = "Scope not close to 0,0";
                }
                MessageBoxResult boxResult = MessageBox.Show(
                    checkStartPoint,
                    checkStartPointHeader,
                    MessageBoxButton.OKCancel);

                if (boxResult != MessageBoxResult.OK) {
                    return;
                }
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
                        if (ADP_Tools.AboveHorizon(
                                altAzTarget,
                                profileService.ActiveProfile.AstrometrySettings.Horizon)) {

                            await telescopeMediator.SlewToCoordinatesAsync(altAzTarget, token);
                            service.Show(PlateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_SolveAndSync_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);
                            if (cameraMediator.GetInfo().Connected) {
                                if (IsPaused) { await pauseTask(); }
                                PlateSolveResult result = await DoSolve(progress, token);
                                if (!result.Success) {
                                    Notification.ShowWarning($"Plate solve failed at Az: {targetAz}, Alt: {nextAlt}");
                                } else {
                                    Coordinates resultCoordinates = result.Coordinates.Transform(Epoch.JNOW);
                                    string addAlignmentResponse = telescopeMediator.Action("Telescope:AddAlignmentReference", $"{resultCoordinates.RA}:{resultCoordinates.Dec}");
                                    ModelPoints.Add(resultCoordinates);
                                }
                            } else {
                                ModelPoints.Add(telescopeMediator.GetInfo().Coordinates);

                            }
                        } else {
                            Notification.ShowWarning($"Target at Az: {targetAz}, Alt: {nextAlt} is below the horizon");
                        }
                        StepCount++;
                    }
                }

            } catch (OperationCanceledException) {
                Notification.ShowWarning("Alignment model buider cancelled");
            } finally {
                service.DelayedClose(new TimeSpan(0, 0, 10));
                IsReadOnly = false;
            }

        }

        protected virtual async Task<PlateSolveResult> DoSolve(IProgress<ApplicationStatus> progress, CancellationToken token) {
            var plateSolver = plateSolverFactory.GetPlateSolver(profileService.ActiveProfile.PlateSolveSettings);
            var blindSolver = plateSolverFactory.GetBlindSolver(profileService.ActiveProfile.PlateSolveSettings);

            var solver = plateSolverFactory.GetCaptureSolver(plateSolver, blindSolver, imagingMediator, filterWheelMediator);
            var parameter = new CaptureSolverParameter() {
                Attempts = SolveAttempts,
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
            if (!Regex.IsMatch(scopeInfo.Name ?? "", "CPWI")) {
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
            return $"Category: Docakables, Item: {nameof(CreateAlignmentModelVM)}";
        }
    }

}
