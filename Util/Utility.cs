#region "copyright"

/*
    Copyright © 2021 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using EDSDKLib;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;

namespace LenseAF.Util
{
    public class Utility
    {
        public string GetCamName(IntPtr cam)
        {
            uint err = EDSDK.EdsGetDeviceInfo(cam, out EDSDK.EdsDeviceInfo info);
            if (EDSDK.EDS_ERR_OK == err)
            {
                return info.szDeviceDescription;
            }
            return null;
        }

        public List<IntPtr> GetConnectedCams()
        {
            List<IntPtr> cams = new List<IntPtr>();
            try
            {
                uint err = EDSDK.EdsGetCameraList(out IntPtr cameraList);
                if (err == EDSDK.EDS_ERR_OK)
                {
                    err = EDSDK.EdsGetChildCount(cameraList, out int count);

                    for (int i = 0; i < count; i++)
                    {
                        err = EDSDK.EdsGetChildAtIndex(cameraList, i, out IntPtr cam);
                        err = EDSDK.EdsGetDeviceInfo(cam, out EDSDK.EdsDeviceInfo info);

                        cams.Add(cam);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            return cams;
        }
    }
}