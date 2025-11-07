#region "copyright"

/*
    Copyright © 2024 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.Input;
using LensAF.Properties;
using LensAF.Util;
using Nikon;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using System;
using System.Collections.Generic;
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

        public double StepSize { get; } = 1;

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

        private int _maxIncrement = 10000;
        public int MaxIncrement
        {
            get => _maxIncrement;
            set { _maxIncrement = value; RaisePropertyChanged(); }
        }

        private int _maxStep = 10000;
        public int MaxStep
        {
            get => _maxStep;
            set { _maxStep = value; RaisePropertyChanged(); }
        }

        public bool TempCompAvailable { get; set; } = false;

        public bool TempComp { get; set; } = false;

        public double Temperature { get; set; } = double.NaN;

        public bool HasSetupDialog { get; set; } = false;

        public string Id { get; set; } = string.Empty;

        public string Category { get; set; } = "Nikon";

        public string Description { get; set; } = "A lens driver for lenses attached to Nikon bodies";

        public string DriverInfo { get; set; } = string.Empty;

        public string DriverVersion { get; set; } = "1.0.0.0";

        public IList<string> SupportedActions { get; set; }

        public string DisplayName { get; set; } = "Nikon Lens Driver";

        public AsyncRelayCommand CalibrateLens { get; set; }
        public RelayCommand CancelCalibrate { get; set; }

        private CancellationTokenSource calibrationToken;

        private NikonDevice Camera
        {
            get => Utility.GetNikonCamera(LensAF.Camera);
        }

        public NikonFocuser(string id)
        {
            Id = id;

            DisplayName = $"Nikon Lens Driver ({id})";

            CalibrateLens = new AsyncRelayCommand(async () =>
            {
                calibrationToken = new CancellationTokenSource();
                try
                {
                    await CalibrateCamera(calibrationToken.Token);
                }
                catch (OperationCanceledException e)
                {
                    Notification.ShowInformation("Calibration canceled");
                    Logger.Info($"Calibration canceled: {e.Message}");
                }
            });
            CancelCalibrate = new RelayCommand(() =>
            {
                calibrationToken?.Cancel();
            });
        }

        private async Task<bool> DriveManualFocus(eNkMAIDMFDrive direction, CancellationToken ct)
        {
            // Start driving the manual focus motor
            Camera.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)direction);

            return await AwaitDeviceReady(ct);
        }

        /**
         * Waits for the camera to enter the DeviceReady state.
         * 
         * Returns true if the camera is ready and false if the timeout expired.
         */
        private async Task<bool> AwaitDeviceReady(CancellationToken ct)
        {
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
                        await Task.Delay(50, ct);
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

        public async Task<bool> Connect(CancellationToken ct)
        {
            if (!ValidateCamera())
            {
                return Connected;
            }

            await CalibrateCamera(ct);

            LensAF.AddLensConfigIfNecessary(DisplayName);

            Connected = true;
            return Connected;
        }

        public async Task CalibrateCamera(CancellationToken ct)
        {
            async Task<bool> TryMove(int position, CancellationToken ct)
            {
                // Go to the closest possible position.
                while (await MoveRelative(-MaxIncrement, ct))
                {
                    ct.ThrowIfCancellationRequested();
                }

                // Try to move to the requested position.
                return await MoveRelative(position, ct);
            }

            LensAF.AddLensConfigIfNecessary(DisplayName);

            NikonRange driveStep = Camera.GetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep);
            MaxIncrement = (int)driveStep.Max;
            Position = (int)driveStep.Max;
            MaxStep = (int)driveStep.Max;
            MaxStep = 0;

            // This moves the camera to the near end of the focus range and
            // sets the position to 0.
            await Move(0, ct);
            int increment = MaxIncrement;

            // Perform a binary search to find the maximum allowed position.
            while (increment > 0)
            {
                do
                {
                    MaxStep += increment;
                } while (await TryMove(MaxStep, ct));

                MaxStep -= increment;
                increment /= 8;
            }

            // Then move close to the far end of the focus range,
            // since this is likely where we want to be.
            Position = (int)(MaxStep * 0.98);
            await TryMove(Position, ct);

            int focusPosition = LensAF.GetFocusPosition(Name);
            if (focusPosition > 0)
            {
                await Move(focusPosition, ct);
            }
        }

        public void Disconnect()
        {
            Connected = false;
        }

        public void Halt()
        {
            return;
        }

        private static bool ValidateCamera()
        {
            List<string> validation = Utility.ValidateNikon(LensAF.Camera);

            foreach (string issue in validation)
            {
                Notification.ShowError(issue);
            }

            return validation.Count == 0;
        }

        public async Task Move(int position, CancellationToken ct, int waitInMs = 1000)
        {
            if (!ValidateCamera())
            {
                return;
            }

            int diff = position - Position;
            await MoveRelative(diff, ct, waitInMs);
            Position = position;
        }

        private async Task<bool> MoveRelative(int diff, CancellationToken ct, int waitInMs = 1000)
        {
            if (Camera.LiveViewEnabled == false)
            {
                Camera.LiveViewEnabled = true;  // required for manual focus
            }

            var direction = eNkMAIDMFDrive.kNkMAIDMFDrive_ClosestToInfinity;

            if (diff < 0)
            {
                direction = eNkMAIDMFDrive.kNkMAIDMFDrive_InfinityToClosest;
                diff = -diff;
            }

            try
            {
                IsMoving = true;

                while (diff > 0)
                {
                    NikonRange range = Camera.GetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep);
                    range.Value = Math.Min(diff, range.Max);
                    diff -= (int)range.Value;
                    Camera.SetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep, range);
                    if (!await DriveManualFocus(direction, ct))
                    {
                        return false;
                    }
                    ct.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                IsMoving = false;
            }

            return true;
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