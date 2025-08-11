# Add To Alignment Model for CPWI Alt-Az Mounts
This plug-in can be used to create, or add to, an alignment model for users of CPWI controlled Alt-Az mounts.

## Why?
The plugin came about due to eye health challenges making it very difficult to use the red dot finder to pick alignment
stars for centring in an eye piece or image. Using this plugin makes it possible to generate an alignment model within
CPWI without any visual observing. 

Having a good alignment model improves mount tracking greatly reducing the number of recentreing actions after plate solving
whether retargeting or due to drift in sequences.

From my understanding of various forum posts and observations the Sync after Solve "Sync" process updates where CPWI thinks the mount
is pointing but does not update the alignment model.

## Brief explanation
The user can select the number of points in both Azimuth and Altitude. Once triggered the mount is moved to the selected
cordinates and an image obtained and plate solved to obtain the actual RA/Dec which are then fed back to CPWI as an alignment location.

I also include sequencer actions to plate solve an image and update the alignment model. Potentially these could be called from a trigger but I have not yet had time to create a dedicated trigger.

The plugin has been developed and tested using CPWI with a Celestron Astro-Fi 6 mount and scope. It is beleived it will 
work with other CPWI controlled Alt Azimuth mounts but it cannot be guranteed.
