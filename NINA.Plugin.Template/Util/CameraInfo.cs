#region "copyright"

/*
    Copyright © 2024 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using EDSDKLib;
using Nikon;
using NINA.Equipment.Equipment.MyCamera;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static EDSDKLib.EDSDK;

namespace LensAF.Util
{
    public class CameraInfo
    {
        private static readonly Dictionary<int, string> AVMap = new Dictionary<int, string>
        {
            { 0x00, null },
            { 0x08, "1" },
            { 0x40, "11" },
            { 0x0B, "1.1" },
            { 0x43, "13" },
            { 0x0C, "1.2" },
            { 0x44, "13" },
            { 0x0D, "1.2" },
            { 0x45, "14" },
            { 0x10, "1.4" },
            { 0x48, "16" },
            { 0x13, "1.6" },
            { 0x4B, "18" },
            { 0x14, "1.8" },
            { 0x4C, "19" },
            { 0x15, "1.8 (1/3)" },
            { 0x4D, "20" },
            { 0x18, "2" },
            { 0x50, "22" },
            { 0x1B, "2.2" },
            { 0x53, "25" },
            { 0x1C, "2.5" },
            { 0x54, "27" },
            { 0x1D, "2.5 (1/3)" },
            { 0x55, "29" },
            { 0x20, "2.8" },
            { 0x58, "32" },
            { 0x23, "3.2" },
            { 0x5B, "36" },
            { 0x24, "3.5" },
            { 0x5C, "38" },
            { 0x25, "3.5 (1/3)" },
            { 0x5D, "40" },
            { 0x28, "4" },
            { 0x60, "45" },
            { 0x2B, "4.5" },
            { 0x63, "51" },
            { 0x2C, "4.5 (1/3)" },
            { 0x64, "54" },
            { 0x2D, "5.0" },
            { 0x65, "57" },
            { 0x30, "5.6" },
            { 0x68, "64" },
            { 0x33, "6.3" },
            { 0x6B, "72" },
            { 0x34, "6.7" },
            { 0x6C, "76" },
            { 0x35, "7.1" },
            { 0x6D, "80" },
            { 0x38, "8" },
            { 0x70, "91" },
            { 0x3B, "9" },
            { 0x3C, "9.5" },
            { 0x3D, "10" },
            { 0xfffffff, null }
        };

        private static float avToFloat(string av)
        {
            if (av.Contains("(1/3)"))
            {
                av = av.Replace("(1/3)", "");
            }
            return float.Parse(av, CultureInfo.InvariantCulture);
        }

        public static string AV(int v)
        {
            return AVMap.TryGetValue(v, out var value) ? value : null;
        }

        public static List<string> GetAVs(IntPtr camera)
        {
            List<string> avs = new List<string>();
            EDSDK.EdsGetPropertyDesc(camera, EDSDK.PropID_Av, out EDSDK.EdsPropertyDesc desc);
            foreach (var item in desc.PropDesc)
            {
                string a = AV(item);
                if (a != null)
                {
                    if (avToFloat(a) >= avToFloat(avs.LastOrDefault("-1")))
                    {
                        avs.Add(a);
                    }
                }
            }
            return avs;
        }

        public static List<string> GetAVs(NikonDevice camera)
        {
            List<string> avs = new List<string>();
            NikonEnum apertures = camera.GetEnum(eNkMAIDCapability.kNkMAIDCapability_Aperture);
            for (int i = 0; i < apertures.Length; i++)
            {
                avs.Add(apertures[i].ToString());
            }
            return avs;
        }

        public string LensName;
        public string CameraName;
        public string CameraFirmware;
        public CameraInfo(IntPtr camera)
        {
            EDSDK.EdsGetPropertyData(camera, 0x0000040d, 0, out LensName);
            EDSDK.EdsGetPropertyData(camera, 0x00000002, 0, out CameraName);
            EDSDK.EdsGetPropertyData(camera, 0x00000007, 0, out CameraFirmware);

            isCanon = true;
        }

        public CameraInfo(NikonDevice camera)
        {
            try
            {
                CameraFirmware = camera.GetString(eNkMAIDCapability.kNkMAIDCapability_Firmware);
                LensName = camera.GetString(eNkMAIDCapability.kNkMAIDCapability_LensInfo);
                CameraName = camera.GetString(eNkMAIDCapability.kNkMAIDCapability_Name);
            }
            catch
            {
                CameraFirmware = "Unknown";
                LensName = "Nikon Lens";
                CameraName = "Nikon Camera";
            }
        }

        private bool isCanon = false;

        public void SetAperture(nint camera, string aperture)
        {
            if (isCanon)
            {
                int ap = 0;
                if (AVMap.ContainsValue(aperture))
                {
                    ap = AVMap.Where(x => x.Value == aperture).First().Key;
                }
                //get size of property
                EdsGetPropertySize(camera, PropID_Av, 0, out EdsDataType proptype, out int propsize);
                //set given property
                EdsSetPropertyData(camera, PropID_Av, 0, propsize, ap);
            }
        }

        public void SetAperture(NikonDevice camera, string aperture)
        {
            if (!isCanon)
            {
                var e = camera.GetEnum(eNkMAIDCapability.kNkMAIDCapability_Aperture);
                e.Index = GetAVs(camera).IndexOf(aperture);
                camera.SetEnum(eNkMAIDCapability.kNkMAIDCapability_Aperture, e);
            }
        }
    }
}
