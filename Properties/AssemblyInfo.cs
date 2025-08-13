using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("879618d2-0a61-4b09-b14e-ef2b55583b84")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("0.0.0.5")]
[assembly: AssemblyFileVersion("0.0.0.5")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("Add To CPWI Alignment Model")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("Create CPWI alignment model")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("ADPUK")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("Add To CPWI Alignment Model")]
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
[assembly: AssemblyMetadata("Homepage", "https://github.com/DalePage/AddToAlignmentModel_NINA_Plugin")]

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "CPWI,Alignment,Celestron,AltAz,Alt-Az,Alignement Model")]

//[Optional] A link that will show a log of all changes in between your plugin's versions
[assembly: AssemblyMetadata("ChangelogURL", "")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
//[Optional] A url to an example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"This plugin appears in the imaging tab and can be used to create, or add to, 
an alignment model for users of CPWI controlled Alt-Az mounts.

Adding points to the alignment model greatly improves the accuracy of the mount when slewing to targets. After plate solving during the alignment process the 
plugin displays the target and actual RA/Dec values. For the initial couple of points these may be quite different, but as more points are added,
the accuracy of the mount improves significantly.

The user can select the number of points in both Azimuth and Altitude. Once triggered the mount is moved to the selected
cordinates and an image obtained and plate solved with the actual RA/Dec then fed back to CPWI as an alignment location.

Also included are sequencer actions to plate solve an image and update the alignment model. From my understanding
of various forum posts and observations the mount ""Sync"" command in CPWI does not update the alignment model and this is an attempt
to overcome this challenge. Potentially these could be called from a trigger but I have not yet had time to do that yet.

The plugin has been developed and tested using CPWI with a Celestron Astro-Fi 6 mount and scope. It is beleived it will 
work with other CPWI controlled Alt Azimuth mounts but it cannot be guranteed.

For the brave at heart there is an option to try turn off the check that a Alt-Az mount is in use. As I don't have access to CPWI
connected equatorial it is untested!

##Instructions##

These instructions assume that your camera and scope are approximately in focus before you start.

1. Connect your scope to your computer using CPWI.
2. In CPWI, when asked about loading models, select *Manual Alignment*
    1. Follow the intructions to point the scope at the appropriate (north in nothern lattitudes, south in southern lattitudes) horizon.
    2. Contine to follow the CPWI instructions until it asks for the first alignment star. At this point you can exit the alignment process.
in CPWI, but, ***keep CPWI running***. 
3. If you have not already done so Start NINA.
4. Connect NINA to at least the mount and camera.
5. Goto the imaging tab select the *Add To CPWI Alignment Model* plugin.
6. Set the number of points you want to add in both Azimuth and Altitude.
    1. The plugin will attempt to add the points in a grid pattern, so if you select 3 points in Azimuth and 3 points in Altitude it will add 9 points.
    2. If you select 1 point in Azimuth and 1 point in Altitude it will add a single point.
    3. The plugin will automatically attempt to divide the azimuth points over the full 360 degree horizon.
7. Set the lowest and highest altidude angle for that the Altitude points should be spread over.
    1. The plugin will attempt to spread the points evenly over the range you select. If you have a custom horizon for your profile then during the alignment process the software will skip any points that are below the horizon. 
8. There are some additional options that can be set before starting the alignment process.
    1. Solve attempts are the number of times an image and plate solve operation will be carried out at a point before it is skipped.
    2. After each solve attempt the solver user interace will be shown to allow you to see the image. The interface will close after *Delay before closing plate solve window* seconds.
8. Press start to begin the alignment process. Please note that the start button is disabled if the mount is not connected via CPWI, the mount is not an Alt-Az mount, or the camera not connected.
    1. The plugin will move the mount to the selected coordinates and take an image.
    2. The image will be plate solved and the actual RA/Dec will be calculated.
    3. The actual RA/Dec will be sent to CPWI as a new alignment point.
    4. The plugin will then move to the next point and repeat the process until all points have been added or, those below the horizon, skipped.
9. On the plugin description screen is the ability to enable use of this plug in with equatorial mounts. This should be used with care and the mount monitored carefully to ensure no colisions occur as it has not been tested with such mounts.

## Acknowledgements##
Acknowledgements to the N.I.N.A. team for their work on the N.I.N.A. software and the plugin framework that this plugin is built on.
Acknowledgements to all the various plugin developers who have provided examples and inspiration for this plugin.

##Disclaimers##
This plugin was developed by Dale Page, ADPUK, and is not affiliated with Celestron or PlaneWave Instruments. Celestron PWI (CPWI) was co-developed by PlaneWave Instruments and Celestron.
Please check the CPWI support page for any changes or updates to the CPWI software.

This plugin is provided as-is and the author makes no guarantees about its functionality or compatibility with any specific hardware or software configurations. Use at your own risk.")]


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
