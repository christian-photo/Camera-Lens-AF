#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using EDSDKLib;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Interfaces.Mediator;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel.Equipment.Camera;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace LensAF.Util
{
    public static class Utility
    {
        public static object GetInstanceField<T>(T instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField;
            FieldInfo field = typeof(T).GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        public static IntPtr GetCamera(ICameraMediator camera)
        {
            try
            {
                CameraVM cameraVM = (CameraVM)GetInstanceField((CameraMediator)camera, "handler");
                if (cameraVM.DeviceChooserVM.SelectedDevice.Category != "Canon")
                {
                    Notification.ShowError("No canon camera connected");
                    return IntPtr.Zero;
                }
                return (IntPtr)GetInstanceField((EDCamera)cameraVM.DeviceChooserVM.SelectedDevice, "_cam");
            } catch (Exception e)
            {
                Logger.Error(e);
                Notification.ShowError(e.Message);
                return IntPtr.Zero;
            }
        }

        public static List<string> Validate(ICameraMediator Camera)
        {
            List<string> error = new List<string>();
            bool cameraConnected = Camera.GetInfo().Connected;

            CameraVM cameraVM = (CameraVM)Utility.GetInstanceField((CameraMediator)Camera, "handler");

            if (!(cameraVM.DeviceChooserVM.SelectedDevice.Category == "Canon" && cameraConnected))
            {
                error.Add("No Canon camera connected");
            }

            return error;
        }
    }
}