#region "copyright"

/*
    Copyright © 2024 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Dasync.Collections;
using LensAF.Properties;
using LensAF.Util;
using Nikon;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Interfaces;
using NINA.Image.Interfaces;
using System.Collections.Generic;
using System.Data.Entity.Spatial;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace LensAF
{
    public class NikonFocuser : BaseINPC, IFocuser
    {
        private bool _isMoving = false;
        public bool IsMoving
        {
            get => _isMoving;
            set
            {
                _isMoving = value;
                RaisePropertyChanged();
            }
        }

        private int _position = Settings.Default.FocusStopPosition;
        public int Position
        {
            get => _position;
            set
            {
                _position = value;
                RaisePropertyChanged();
            }
        }

        private bool _connected;
        public bool Connected
        {
            get => _connected;
            set
            {
                _connected = value;
                RaisePropertyChanged();
            }
        }

        private double _stepSize = 1;
        public double StepSize
        {
            get => _stepSize;
            set
            {
                _stepSize = value;
                Settings.Default.Resolution = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        private string _name = "Nikon Lens Driver";
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                RaisePropertyChanged();
            }
        }

        public int MaxIncrement { get; set; } = 10000;

        public int MaxStep { get; set; } = 10000;

        public bool TempCompAvailable { get; set; } = false;

        public bool TempComp { get; set; } = false;

        public double Temperature { get; set; } = double.NaN;

        public bool HasSetupDialog { get; set; } = false;

        public string Id { get; set; }

        public string Category { get; set; } = "Nikon";

        public string Description { get; set; } = "A lens driver for lenses attached to Nikon bodies";

        public string DriverInfo { get; set; } = string.Empty;

        public string DriverVersion { get; set; } = "1.0.0.0";

        public IList<string> SupportedActions { get; set; }

        public string DisplayName { get; set; } = "Nikon Lens Driver";

        private NikonDevice Camera
        {
            get => Utility.GetNikonCamera(LensAF.Camera);
        }

        public NikonFocuser(string id)
        {
            Id = id;
            NikonRange driveStep = Camera.GetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep);
            MaxStep = (int)driveStep.Max;
            MaxIncrement = (int)driveStep.Max;
            StepSize = 10;
        }
        private void DriveManualFocus(eNkMAIDMFDrive direction, CancellationToken ct)
        {
            if (!ValidateCamera())
            {
                return;
            }

            if (Camera.LiveViewEnabled == false)
            {
                Camera.LiveViewEnabled = true;  // required for manual focus
            }

            // Start driving the manual focus motor
            Camera.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)direction);

            AwaitDeviceReady(ct);
        }

        /**
         * Waits for the camera to enter the DeviceReady state.
         * 
         * Returns true if the camera is ready and false if the timeout expired.
         */
        private bool AwaitDeviceReady(CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                try
                {
                    Camera.Start(eNkMAIDCapability.kNkMAIDCapability_DeviceReady);
                    return true;
                }
                catch (NikonException e)
                {
                    if (e.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
                        Task.Delay(50, ct);
                        ct.ThrowIfCancellationRequested();
                        continue;
                    }

                    if (e.ErrorCode == eNkMAIDResult.kNkMAIDResult_MFDriveEnd)
                    {
                        Logger.Debug("Device at the end of the focus range.");
                        return false;
                    }

                    throw;
                }
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            return string.Empty;
        }

        public Task<bool> Connect(CancellationToken token)
        {
            return Task.Run(() =>
            {
                List<string> errors = Utility.ValidateNikon(LensAF.Camera);
                if (errors.Count > 0)
                {
                    foreach (string error in errors)
                    {
                        Notification.ShowError(error);
                    }
                    return Connected;
                }

                Connected = true;
                return Connected;
            });
        }

        public void Disconnect()
        {
            Connected = false;
        }

        public void Halt()
        {
            return;
        }

        private bool ValidateCamera()
        {
            List<string> validation = Utility.ValidateNikon(LensAF.Camera);

            foreach (string issue in validation)
            {
                Notification.ShowError(issue);
                Logger.Error($"Cannot move focus: {issue}");
            }

            return validation.Count == 0;
        }

        public async Task Move(int position, CancellationToken ct, int waitInMs = 1000)
        {
            if (!ValidateCamera())
            {
                return;
            }

            double diff = Position - position;

            await Task.Run(() =>
            {
                if (diff > 0) // Drive focus near
                {
                    NikonRange range = Camera.GetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep);
                    range.Value = diff;
                    Camera.SetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep, range);
                    DriveManualFocus(eNkMAIDMFDrive.kNkMAIDMFDrive_InfinityToClosest, ct);
                }
                else // Drive focus far
                {
                    NikonRange range = Camera.GetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep);
                    range.Value = -diff;
                    Camera.SetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep, range);
                    DriveManualFocus(eNkMAIDMFDrive.kNkMAIDMFDrive_ClosestToInfinity, ct);
                }

                Position = (int)position;
                ct.ThrowIfCancellationRequested();
            });
        }

        public void SendCommandBlind(string command, bool raw = true)
        {
            return;
        }

        public bool SendCommandBool(string command, bool raw = true)
        {
            return false;
        }

        public string SendCommandString(string command, bool raw = true)
        {
            return string.Empty;
        }

        public void SetupDialog()
        {
            Logger.Warning("Nikon lens driver SetupDialog not implemented");
            return;
        }
    }
}