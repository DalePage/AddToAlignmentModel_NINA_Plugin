using CommunityToolkit.Mvvm.ComponentModel;
using NINA.Astrometry;
using NINA.PlateSolving;
using System;
using System.Collections.ObjectModel;
using System.Net.Security;

namespace ADPUK.NINA.AddToAlignmentModel {
    public partial class ModelPoint : ObservableObject {

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetAltString))]
        [NotifyPropertyChangedFor(nameof(SeparationString))]
        [NotifyPropertyChangedFor(nameof(Separation))]
        private double _TargetAlt;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetAzString))]
        [NotifyPropertyChangedFor(nameof(SeparationString))]
        [NotifyPropertyChangedFor(nameof(Separation))]
        private double _TargetAz;

        [ObservableProperty]
        private string _TargetRAString;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SeparationString))]
        [NotifyPropertyChangedFor(nameof(Separation))]
        private double _TargetRA;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetDecString))]
        [NotifyPropertyChangedFor(nameof(SeparationString))]
        [NotifyPropertyChangedFor(nameof(Separation))]
        private double _TargetDec;

        [ObservableProperty]
        private string _ActualRAString;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SeparationString))]
        [NotifyPropertyChangedFor(nameof(Separation))]
        private double _ActualRA;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActualDecString))]
        [NotifyPropertyChangedFor(nameof(SeparationString))]
        [NotifyPropertyChangedFor(nameof(Separation))]
        private double _ActualDec;



        public double Separation {
            get {
                if ((TargetRA == 0d  && TargetDec == 0d) || (TargetAlt == 0d && TargetAz == 0d)) {
                    return 0d;
                }
                return Math.Sqrt(Math.Pow((TargetRA - ActualRA), 2) + Math.Pow((TargetDec - ActualDec), 2));
            }
        }

        public string TargetAltString {
            get {
                if (TargetAlt == 0d) {
                    return "*";
                }
                return AstroUtil.DegreesToDMS(TargetAlt);
            }
            set {
                TargetAltString = value;
                OnPropertyChanged(nameof(TargetAltString));
            }
        }
        public string TargetAzString {
            get {
                if (TargetAz == 0d) {
                    return "*";
                }
                return AstroUtil.DegreesToDMS(TargetAz);
            }
            set {
                TargetAltString = value;
                OnPropertyChanged($"{nameof(TargetAzString)}");
            }
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

        public ModelPoint(Coordinates targetCoords, PlateSolveResult result) {
            TargetAlt = 0d;
            TargetAz = 0d;
            TargetRAString = targetCoords.RAString;
            TargetRA = targetCoords.RA;
            TargetDec = targetCoords.Dec;
            ActualRA = result.Coordinates.RADegrees;
            ActualRAString = result.Coordinates.RAString;
            ActualDec = result.Coordinates.Dec;
        }
        public ModelPoint(Coordinates targetCoords) {
            TargetAlt = 0d;
            TargetAz = 0d;
            TargetRAString = targetCoords.RAString;
            TargetRA = targetCoords.RA;
            TargetDec = targetCoords.Dec;
            ActualRAString = string.Empty;
            ActualRA = 0d;
            ActualDec = 0d;
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
        }
    }
    public class ListModelModelPoints : ObservableCollection<ModelPoint> {
        public ListModelModelPoints() : base() { }
    }
}

