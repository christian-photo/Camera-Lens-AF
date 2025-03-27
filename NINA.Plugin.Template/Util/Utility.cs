#region "copyright"

/*
    Copyright © 2025 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using EDSDKLib;
using Nikon;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

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
                List<string> errors = Validate(camera);
                if (errors.Count > 0)
                {
                    foreach (string error in errors)
                    {
                        Notification.ShowError(error);
                    }
                    return IntPtr.Zero;
                }
                IDevice cam = camera.GetDevice() is PersistSettingsCameraDecorator decorator ? decorator.Camera : camera.GetDevice();
                return (IntPtr)GetInstanceField((EDCamera)cam, "_cam");
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Notification.ShowError(e.Message);
                return IntPtr.Zero;
            }
        }
        public static EDCamera GetCanon(ICameraMediator camera)
        {
            try
            {
                List<string> errors = Validate(camera);
                if (errors.Count > 0)
                {
                    foreach (string error in errors)
                    {
                        Notification.ShowError(error);
                    }
                    return null;
                }
                IDevice cam = camera.GetDevice() is PersistSettingsCameraDecorator decorator ? decorator.Camera : camera.GetDevice();
                return (EDCamera)cam;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Notification.ShowError(e.Message);
                return null;
            }
        }
        public static NikonDevice GetNikonCamera(ICameraMediator camera)
        {
            try
            {
                List<string> errors = ValidateNikon(camera);
                if (errors.Count > 0)
                {
                    foreach (string error in errors)
                    {
                        Notification.ShowError(error);
                    }
                    return null;
                }
                IDevice cam = camera.GetDevice() is PersistSettingsCameraDecorator decorator ? decorator.Camera : camera.GetDevice();
                return (NikonDevice)GetInstanceField((NikonCamera)cam, "_camera");
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Notification.ShowError(e.Message);
                return null;
            }
        }

        public static List<string> Validate(ICameraMediator Camera)
        {
            List<string> error = new List<string>();
            bool cameraConnected = Camera.GetInfo().Connected;

            if (!cameraConnected)
            {
                error.Add("No camera connected");
                return error;
            }

            if (!(Camera.GetDevice().Category == "Canon" && cameraConnected))
            {
                error.Add("No Canon camera connected");
            }

            return error;
        }

        public static List<string> ValidateNikon(ICameraMediator Camera)
        {
            List<string> error = new List<string>();
            bool cameraConnected = Camera.GetInfo().Connected;

            if (!cameraConnected)
            {
                error.Add("No camera connected");
                return error;
            }

            if (!(Camera.GetDevice().Category == "Nikon" && cameraConnected))
            {
                error.Add("No Nikon camera connected");
            }

            return error;
        }

        public static string ErrorCodeToString(uint error)
        {
            string errStr;
            if (EDSDKLocal.ErrorCodes.ContainsKey(error))
            {
                errStr = EDSDKLocal.ErrorCodes[error];
            }
            else
            {
                errStr = $"Unknown ({error})";
            }

            return errStr;
        }
    }
}