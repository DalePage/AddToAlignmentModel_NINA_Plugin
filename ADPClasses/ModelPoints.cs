using CommunityToolkit.Mvvm.ComponentModel;
using NINA.Astrometry;
using NINA.PlateSolving;
using System.Collections.ObjectModel;

namespace ADPUK.NINA.AddToAlignmentModel {
    public partial class ModelPoint : ObservableObject {

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetAltString))]
        private double _TargetAlt;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetAzString))]
        private double _TargetAz;
        [ObservableProperty]
        private string _TargetRAString;
        [ObservableProperty]
        private double _TargetRA;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetDecString))]
        private double _TargetDec;
        [ObservableProperty]
        private string _ActualRAString;
        [ObservableProperty]
        private double _ActualRA;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActualDecString))]
        private double _ActualDec;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SeparationString))]
        private double _Separation;

        public string TargetAltString {
            get => AstroUtil.DegreesToDMS(TargetAlt);
        }
        public string TargetAzString {
            get => AstroUtil.DegreesToDMS(TargetAz);
        }
        public string TargetDecString {
            get => AstroUtil.DegreesToDMS((double)TargetDec);
        }
        public string SeparationString {
            get => AstroUtil.DegreesToDMS(Separation);
        }

        public string ActualDecString {
            get => AstroUtil.DegreesToDMS(ActualDec);
        }

        public ModelPoint() { }

        public ModelPoint(Coordinates targetCoords) {
            TargetAlt = 0d;
            TargetAz = 0d;
            TargetRAString = targetCoords.RAString;
            TargetRA = targetCoords.RA;
            TargetDec = targetCoords.Dec;
            ActualRAString = string.Empty;
            ActualRA = 0d;
            ActualDec = 0d;
            Separation = 0d;
        }

        public ModelPoint(TopocentricCoordinates target) {
            Coordinates targetCoords = target.Transform(Epoch.JNOW);
            TargetAlt = target.Altitude.Degree;
            TargetAz = target.Azimuth.Degree;
            TargetRAString = targetCoords.RAString;
            TargetRA = targetCoords.RA;
            TargetDec = targetCoords.Dec;
            ActualRAString = string.Empty;
            ActualRA = 0d;
            ActualDec = 0d;
            Separation = 0d;

        }
        public ModelPoint(TopocentricCoordinates target, PlateSolveResult plateSolveResult) {
            Coordinates result = plateSolveResult.Coordinates.Transform(Epoch.JNOW);
            Coordinates targetCoords = target.Transform(Epoch.JNOW);
            TargetAlt = target.Altitude.Degree;
            TargetAz = target.Azimuth.Degree;
            TargetRAString = targetCoords.RAString;
            TargetRA = targetCoords.RA;
            TargetDec = targetCoords.Dec;
            ActualRAString = result.RAString;
            ActualRA = result.RA;
            ActualDec = result.Dec;
            Separation = plateSolveResult.Separation.Distance.Degree;
        }
    }
    public class ListModelModelPoints : ObservableCollection<ModelPoint> {
        public ListModelModelPoints() : base() { }
    }
}

