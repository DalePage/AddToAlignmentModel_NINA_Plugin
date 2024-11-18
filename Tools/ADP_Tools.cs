using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Profile;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADPUK.NINA.AddToAlignmentModel {
    public class ADP_Tools {
        public static bool AboveHorizon(Coordinates currentPosition, CustomHorizon horizon, double latitude) {
            double currentAlt = AstroUtil.GetAltitude(currentPosition.RADegrees, latitude, currentPosition.Dec);
            double currentAz = AstroUtil.GetAzimuth(currentPosition.RADegrees, currentAlt, latitude, currentPosition.Dec);
            return (currentAlt > horizon.GetAltitude(currentAz));
        }
    }
}
