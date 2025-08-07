using ADPUK.NINA.AddToAlignmentModel.Locales;
using CommunityToolkit.Mvvm.ComponentModel;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.PlateSolving;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Serialization;

namespace ADPUK.NINA.AddToAlignmentModel {
    public partial class ADP_Tools {
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
                i.Add(ViewStrings.RequireCPWI);
            }
            if (scopeInfo.AlignmentMode != AlignmentMode.AltAz && !pluginSettings.GetValueBoolean(nameof(AddToAlignmentModel.EnableEquatorialMounts), false)) {
                i.Add(ViewStrings.AltAzOnly);
            }
            if (!cameraInfo.Connected) {
                i.Add(Loc.Instance["LblCameraNotConnected"]);
            }
            return i;
        }
        public static double ReadyToStart(TelescopeInfo telescopeInfo) {
            double initialAzimuth = 0.0;
            string msgBoxText = ViewStrings.EnsureNorth;
            if (telescopeInfo.SiteLatitude < 0.0) {
                msgBoxText = ViewStrings.EnsureSouth;
                initialAzimuth = 180.0;
            }
            MessageBoxResult boxResult = MessageBox.Show(
                msgBoxText,
                ViewStrings.PreAlignment,
                MessageBoxButton.OKCancel);

            if (boxResult != MessageBoxResult.OK) {
                if (initialAzimuth == 0.0) {
                    throw new SequenceEntityFailedException(ViewStrings.NorthNotConfirmed);
                } else {
                    throw new SequenceEntityFailedException(ViewStrings.SouthNotConfirmed);
                }
            }
            if (Math.Abs(telescopeInfo.Azimuth - initialAzimuth) > 10.0 || Math.Abs(telescopeInfo.Altitude) > 10.0) {
                MessageBoxResult boxResult1 = MessageBox.Show(
                    $"{ViewStrings.ScopeAltAzCoordinates.Replace("{{Azimuth}}", telescopeInfo.AzimuthString).Replace("{{Altitude}}", telescopeInfo.AltitudeString)} {ViewStrings.IsThisCorrect}",
                    ViewStrings.ScopeNotAtZero,
                    MessageBoxButton.OKCancel);
                initialAzimuth = telescopeInfo.Azimuth;

                if (boxResult1 != MessageBoxResult.OK) {
                    throw new SequenceEntityFailedException(ViewStrings.ScopeNotAtZero);
                }
            }
            return initialAzimuth;
        }
        public static CaptureSolverParameter CreateCaptureSolverParameter(IProfile activeProfile, Coordinates currentPosition, int? solveAttempts = null) {
            return new CaptureSolverParameter() {
                Attempts = solveAttempts ?? activeProfile.PlateSolveSettings.NumberOfAttempts,
                Binning = activeProfile.PlateSolveSettings.Binning,
                Coordinates = currentPosition,
                DownSampleFactor = activeProfile.PlateSolveSettings.DownSampleFactor,
                FocalLength = activeProfile.TelescopeSettings.FocalLength,
                MaxObjects = activeProfile.PlateSolveSettings.MaxObjects,
                PixelSize = activeProfile.CameraSettings.PixelSize,
                ReattemptDelay = TimeSpan.FromMinutes(activeProfile.PlateSolveSettings.ReattemptDelay),
                Regions = activeProfile.PlateSolveSettings.Regions,
                SearchRadius = activeProfile.PlateSolveSettings.SearchRadius,
                BlindFailoverEnabled = activeProfile.PlateSolveSettings.BlindFailoverEnabled
            };
        }
    }
    
}
