using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("879618d2-0a61-4b09-b14e-ef2b55583b84")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("0.9.0.1")]
[assembly: AssemblyFileVersion("9.0.0.1")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("Add To CPWI Alt-Az Alignment Model")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("Create CPWI alignment model")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("ADPUK")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("Add To Alignment Model")]
[assembly: AssemblyCopyright("Copyright © 2025 ADPUK")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.1.0.9001")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://github.com/DalePage/AddToAlignmentModel_NINA_Plugin")]

// The following attributes are optional for the official manifest meta data

//[Optional] Your plugin homepage URL - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Celestron CPWI Alignment Model AltAz Alt-Az AZ")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"This plug-in appears in the imaging tab and can be used to create, or add to, 
an alignment model for users of CPWI controlled Alt-Az mounts.

The user can select the number of points in both Azimuth and Altitude. Once triggered the mount is moved to the selected
cordinates and an image obtained and plate solved with the actual RA/Dec then fed back to CPWI as an alignment location.

Also included are sequencer actions to plate solve an image and update the alignment model. From my understanding
of various forum posts and observations the mount ""Sync"" command in CPWI does not update the alignment model and this is an attempt
to overcome this challenge. Potentially these could be called from a trigger but I have not yet had time to do that yet.

The plugin has been developed and tested using CPWI with a Celestron Astro-Fi 6 mount and scope. It is beleived it will 
work with other CPWI controlled Alt Azimuth mounts but it cannot be guranteed.

For the brave at heart there is an option to try turn off the check that a Alt-Az mount is in use. As I don't have access to CPWI connected equatorial it is untested")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]
