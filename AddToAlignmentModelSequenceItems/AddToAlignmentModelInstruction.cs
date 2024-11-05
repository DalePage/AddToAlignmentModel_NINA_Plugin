using ADPUK.NINA.AddToAlignmentModel.Properties;
using ASCOM.Com.DriverAccess;
using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ADPUK.NINA.AddToAlignmentModel.AddToAlignmentModelTestCategory {
    /// <summary>
    /// This Class shows the basic principle on how to add a new Sequence Instruction to the N.I.N.A. sequencer via the plugin interface
    /// For ease of use this class inherits the abstract SequenceItem which already handles most of the running logic, like logging, exception handling etc.
    /// A complete custom implementation by just implementing ISequenceItem is possible too
    /// The following MetaData can be set to drive the initial values
    /// --> Name - The name that will be displayed for the item
    /// --> Description - a brief summary of what the item is doing. It will be displayed as a tooltip on mouseover in the application
    /// --> Icon - a string to the key value of a Geometry inside N.I.N.A.'s geometry resources
    ///
    /// If the item has some preconditions that should be validated, it shall also extend the IValidatable interface and add the validation logic accordingly.
    /// </summary>
    [ExportMetadata("Name", "Add Current Target to Pointing Model")]
    [ExportMetadata("Description", "This plugin assumes a plate solve has been completed and can be used to update the mount pointing model.")]
    [ExportMetadata("Icon", "TelescopeSVG")]
    [ExportMetadata("Category", "Add To Alignment Model")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AddToAlignmentModelInstruction : SequenceItem {
        /// <summary>
        /// The constructor marked with [ImportingConstructor] will be used to import and construct the object
        /// General device interfaces can be added to the constructor parameters and will be automatically injected on instantiation by the plugin loader
        /// </summary>
        /// <remarks>
        /// Available interfaces to be injected:
        ///     - IProfileService,
        ///     - ICameraMediator,
        ///     - ITelescopeMediator,
        ///     - IFocuserMediator,
        ///     - IFilterWheelMediator,
        ///     - IGuiderMediator,
        ///     - IRotatorMediator,
        ///     - IFlatDeviceMediator,
        ///     - IWeatherDataMediator,
        ///     - IImagingMediator,
        ///     - IApplicationStatusMediator,
        ///     - INighttimeCalculator,
        ///     - IPlanetariumFactory,
        ///     - IImageHistoryVM,
        ///     - IDeepSkyObjectSearchVM,
        ///     - IDomeMediator,
        ///     - IImageSaveMediator,
        ///     - ISwitchMediator,
        ///     - ISafetyMonitorMediator,
        ///     - IApplicationMediator
        ///     - IApplicationResourceDictionary
        ///     - IFramingAssistantVM
        ///     - IList<IDateTimeProvider>
        /// </remarks>

        private readonly ITelescopeMediator telescopeMediator;
        private readonly IProfileService profileService;
        private bool hasSynced = false;

        [ImportingConstructor]
        public AddToAlignmentModelInstruction(IProfileService profileService, ITelescopeMediator telescopeMediator) {
            this.profileService = profileService;
            this.telescopeMediator = telescopeMediator;
        }
        public AddToAlignmentModelInstruction() { }
        public AddToAlignmentModelInstruction(AddToAlignmentModelInstruction copyMe) : this() {
            CopyMetaData(copyMe);
        }

        /// <summary>
        /// An example property that can be set from the user interface via the Datatemplate specified in PluginTestItem.Template.xaml
        /// </summary>
        /// <remarks>
        /// If the property changes from the code itself, remember to call RaisePropertyChanged() on it for the User Interface to notice the change
        /// </remarks>
        [JsonProperty]
        public string Text { get; set; }

        /// <summary>
        /// The core logic when the sequence item is running resides here
        /// Add whatever action is necessary
        /// </summary>
        /// <param name="progress">The application status progress that can be sent back during execution</param>
        /// <param name="token">When a cancel signal is triggered from outside, this token can be used to register to it or check if it is cancelled</param>
        /// <returns></returns>
        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            var scope = telescopeMediator.GetDevice();
            if (scope != null && scope.Connected) {
                Coordinates currentTarget = telescopeMediator.GetCurrentPosition();
                string addAlignmentRespose = telescopeMediator.Action("AddAlignmentReference", $"{currentTarget.RA}:{currentTarget.Dec}");
                Notification.ShowSuccess("Target sent to scope alignment model");
            } else {
                Notification.ShowWarning("Scope not connected");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// When items are put into the sequence via the factory, the factory will call the clone method. Make sure all the relevant fields are cloned with the object.
        /// </summary>
        /// <returns></returns>
        public override object Clone() {
            return new AddToAlignmentModelInstruction(this);
        }

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(AddToAlignmentModelInstruction)}, Text: {Text}";
        }
        public virtual bool Validate() {
            var i = new List<string>();
            if (!telescopeMediator.GetInfo().Connected) {
                i.Add(Loc.Instance["LblTelescopeNotConnected"]);
            }
            Issues = i;
            return i.Count == 0;
        }
        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }
        private IList<string> issues = new List<string>();



    }
}