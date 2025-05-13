using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Settings = ADPUK.NINA.AddToAlignmentModel.Properties.Settings;

namespace ADPUK.NINA.AddToAlignmentModel {
    /// <summary>
    /// This class exports the IPluginManifest interface and will be used for the general plugin information and options
    /// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
    /// 
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "AddToAlignmentModel_Options" where AddToAlignmentModel corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class AddToAlignmentModel : PluginBase, INotifyPropertyChanged {
        private readonly PluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;

        [ImportingConstructor]
        public AddToAlignmentModel(IProfileService profileService, IOptionsVM options) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            // This helper class can be used to store plugin settings that are dependent on the current profile
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            this.profileService = profileService;
            // React on a changed profile
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            // Hook in to telescope

        }

        public override Task Teardown() {
            // Make sure to unregister an event when the object is no longer in use. Otherwise garbage collection will be prevented.
            profileService.ProfileChanged -= ProfileService_ProfileChanged;

            return base.Teardown();
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            // Rase the event that this profile specific value has been changed due to the profile switch
            RaisePropertyChanged(nameof(ProfileSpecificNotificationMessage));
        }
        public string DefaultNotificationMessage {
            get {
                return Settings.Default.DefaultNotificationMessage;
            }
            set {
                Settings.Default.DefaultNotificationMessage = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string ProfileSpecificNotificationMessage {
            get {
                return pluginSettings.GetValueString(nameof(ProfileSpecificNotificationMessage), string.Empty);
            }
            set {
                pluginSettings.SetValueString(nameof(ProfileSpecificNotificationMessage), value);
                RaisePropertyChanged();
            }
        }

        public int NumberOfAzimuthPoints {
            get { return pluginSettings.GetValueInt32(nameof(NumberOfAzimuthPoints), 4); }
            set {
                pluginSettings.SetValueInt32(nameof(NumberOfAzimuthPoints), value);
                RaisePropertyChanged("total_Steps");
                RaisePropertyChanged();
            }
        }
        public int NumberOfAltitudePoints {
            get { return pluginSettings.GetValueInt32(nameof(NumberOfAltitudePoints), 2); }
            set {
                pluginSettings.SetValueInt32(nameof(NumberOfAltitudePoints), value);
                RaisePropertyChanged("total_Steps");
                RaisePropertyChanged();
            }
        }
        public double MaxElevation {
            get { return pluginSettings.GetValueDouble(nameof(MaxElevation), 80.0); }
            set {
                pluginSettings.SetValueDouble(nameof(MaxElevation), value);
                RaisePropertyChanged("total_Steps");
                RaisePropertyChanged();
            }
        }

        public double MinElevation {
            get { return pluginSettings.GetValueDouble(nameof(MinElevation), 35.0); }
            set {
                pluginSettings.SetValueDouble(nameof(MinElevation), value);
                RaisePropertyChanged("total_Steps");
                RaisePropertyChanged();
            }
        }
        public int SolveAttempts {
            get { return pluginSettings.GetValueInt32(nameof(SolveAttempts), 1); }
            set { 
                pluginSettings.SetValueInt32(nameof(SolveAttempts), value);
                RaisePropertyChanged();
            }
            
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
