using Accord.MachineLearning;
using CommunityToolkit.Mvvm.ComponentModel;
using NINA.Astrometry;
using NINA.PlateSolving;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.ObjectModel;
using System.Net.Security;

namespace ADPUK.NINA.AddToAlignmentModel {
    public partial class ModelPoint : ObservableObject {

        public Coordinates TargetCoordinates {
            get; set;
        }
        public Coordinates ActualCoordinates {
            get; set;
        }


        public string SeparationString {
            get {
                if (ActualCoordinates is null || TargetCoordinates is null) {
                    return "N/A";
                }
                return (ActualCoordinates - TargetCoordinates).ToString();
            }
        }
        public string Status {
            get; set;
        }

        public string ActualRAString {
            get {
                if (ActualCoordinates is null) {
                    return "N/A";
                }
                return ActualCoordinates.RAString;
                ;
            }
        }
        public string ActualDecString {
            get {
                if (ActualCoordinates is null) {
                    return "N/A";
                }
                return ActualCoordinates.DecString;
            }
        }
        public string TargetRAString {
            get {
                if (TargetCoordinates is null) {
                    return "N/A";
                }
                return TargetCoordinates.RAString;
            }
        }
        public string TargetDecString {
            get {
                if (TargetCoordinates is null) {
                    return "N/A";
                }
                return TargetCoordinates.DecString;
            }
        }
        public ModelPoint() { }

        public ModelPoint(Coordinates targetCoords, PlateSolveResult result, Epoch epoch = Epoch.J2000) {
            TargetCoordinates = targetCoords.Transform(epoch);
            if (result==null) {
                Status = "No Result";
                return;
            }
            ActualCoordinates = result.Coordinates.Transform(epoch);
            Status = result.Success ? "Solved" : "Failed";
        }

    }
    public class ListModelModelPoints : ObservableCollection<ModelPoint> {
        public ListModelModelPoints() : base() { }
    }
}

