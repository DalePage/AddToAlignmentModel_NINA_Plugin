using Accord.Statistics.Filters;
using ADPUK.NINA.AddToAlignmentModel.Locales;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Profile.Interfaces;
using NINA.Core.Enum;
using NINA.Profile;
using NINA.WPF.Base.Mediator;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ADPUK.NINA.AddToAlignmentModel {
    public class ADP_Tools {
        public static bool AboveMinAlt(Coordinates currentPosition, CustomHorizon horizon, double latitude, double minAboveHorizon) {
            double currentAlt = AstroUtil.GetAltitude(currentPosition.RADegrees, latitude, currentPosition.Dec);
            double currentAz = AstroUtil.GetAzimuth(currentPosition.RADegrees, currentAlt, latitude, currentPosition.Dec);
            if (horizon == null) {
                return currentAlt >= minAboveHorizon;
            }
            return (currentAlt >= horizon.GetAltitude(currentAz) + minAboveHorizon);
        }
        public static bool AboveMinAlt(TopocentricCoordinates topocentricCoordinates, CustomHorizon horizon, double minAboveHorizon) {
            double currentAlt = topocentricCoordinates.Altitude.Degree;
            if (horizon == null) { return currentAlt >= minAboveHorizon; }
            return currentAlt >= horizon.GetAltitude(topocentricCoordinates.Azimuth.Degree) + minAboveHorizon;
        }
        public static List<string> ValidateConnections(TelescopeInfo scopeInfo, CameraInfo cameraInfo, IPluginOptionsAccessor pluginSettings) {
            var i = new List<string>();
            if (!scopeInfo.Connected) {
                i.Add($"{Loc.Instance["LblTelescopeNotConnected"]}");
            }
            if (!Regex.IsMatch(scopeInfo.Name ?? "", "CPWI", RegexOptions.IgnoreCase)) {
                i.Add(ViewStrings.CPWI);
            }
            if (scopeInfo.AlignmentMode != AlignmentMode.AltAz && !pluginSettings.GetValueBoolean(nameof(AddToAlignmentModel.EnableEquatorialMounts), false)) {
                i.Add(ViewStrings.AltAzOnly);
            }
            if (!cameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            }
            return i;
        }
    }
}
