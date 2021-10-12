using CUESDK;
using IcueHelper.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace IcueHelper
{
    /// <summary>
    /// Icue SDK handler
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class Sdk : IDisposable
    {
        /// <summary>
        /// Gets the protocol details.
        /// </summary>
        /// <value>
        /// The protocol details.
        /// </value>
        public CorsairProtocolDetails ProtocolDetails { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance has exclusive lighting control.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance has exclusive lighting control; otherwise, <c>false</c>.
        /// </value>
        public bool HasExclusiveLightingControl { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Sdk"/> class.
        /// </summary>
        /// <param name="exclusiveLightingControl">if set to <c>true</c> [exclusive lighting control].</param>
        /// <exception cref="Exception">Incompatible SDK (" + sdkVersion + ") and CUE " + cueVersion + " versions.</exception>
        public Sdk(bool exclusiveLightingControl)
        {
            initialiseSdk(exclusiveLightingControl);
        }

        private void initialiseSdk(bool exclusiveLightingControl)
        {
            ProtocolDetails = CorsairLightingSDK.PerformProtocolHandshake();

            if (ProtocolDetails.ServerProtocolVersion == 0)
            {
                if (!HandleError())
                {
                    //server not found... seep 10 seconds and try again
                    Thread.Sleep(10000);
                    initialiseSdk(exclusiveLightingControl);
                    return;
                }
            }

            if (ProtocolDetails.BreakingChanges)
            {
                String sdkVersion = ProtocolDetails.SdkVersion;
                String cueVersion = ProtocolDetails.ServerVersion;
                throw new Exception("Incompatible SDK (" + sdkVersion + ") and CUE " + cueVersion + " versions.");
            }


            if (exclusiveLightingControl)
            {
                CorsairLightingSDK.RequestControl(CorsairAccessMode.ExclusiveLightingControl);
                HasExclusiveLightingControl = true;
            }

        }

        /// <summary>
        /// Lists the devices.
        /// </summary>
        /// <returns></returns>
        public Device[] ListDevices()
        {
            int deviceCount = CorsairLightingSDK.GetDeviceCount();

            if (deviceCount > 0)
            {
                Device[] devices = new Device[deviceCount];
                for (int i = 0; i < deviceCount; i++)
                {
                    CorsairDeviceInfo deviceInfo = CorsairLightingSDK.GetDeviceInfo(i);
                    CorsairLedPositions positions = GetLedPositions(i);
                    List<Led> leds = new List<Led>();
                    for (int j = 0; j < positions.LedPosition.Length; j++)
                    {
                        leds.Add(new Led(positions.LedPosition[j]));
                    }
                    devices[i] = new Device(deviceInfo, i, leds);
                    RefreshDeviceColor(devices[i]);
                }
                return devices;
            }
            return new Device[0];
        }

        /// <summary>
        /// Gets the led positions.
        /// </summary>
        /// <param name="deviceIndex">Index of the device.</param>
        /// <returns></returns>
        private CorsairLedPositions GetLedPositions(int deviceIndex)
        {
            return CorsairLightingSDK.GetLedPositionsByDeviceIndex(deviceIndex);
        }

        /// <summary>
        /// Refreshes the color of the device.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <returns></returns>
        public bool RefreshDeviceColor(Device device)
        {
            CorsairLedColor[] leds = device.Leds.Select(x => x.CorsairLedColor).ToArray();
            bool result = CorsairLightingSDK.GetLedsColorsByDeviceIndex(device.DeviceIndex, ref leds);
            if (result)
            {
                for (int i = 0; i < leds.Length; i++)
                {
                    device.Leds[i].R = leds[i].R;
                    device.Leds[i].G = leds[i].G;
                    device.Leds[i].B = leds[i].B;
                }
                device.CalculateAverageColor();
            }
            return result;
        }

        /// <summary>
        /// Sets the color of the device.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        public bool SetDeviceColor(Device device, int r, int g, int b)
        {
            device.SetColor(r, g, b);
            CorsairLedColor[] leds = device.Leds.Select(x => x.CorsairLedColor).ToArray();
            bool setResult = CorsairLightingSDK.SetLedsColorsBufferByDeviceIndex(device.DeviceIndex, leds);
            if (setResult)
            {
                return CorsairLightingSDK.SetLedsColorsFlushBuffer();
            }
            
            return false;
        }

        /// <summary>
        /// Sets the color of the device.
        /// </summary>
        /// <param name="devices">The devices.</param>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        /// <returns></returns>
        public bool SetDeviceColor(Device[] devices, int r, int g, int b)
        {
            if (devices == null)
            {
                return false;
            }
            if (devices.Length == 0)
            {
                return true;
            }
            for (int i = 0; i < devices.Length; i++)
            {
                devices[i].SetColor(r, g, b);
                CorsairLedColor[] leds = devices[i].Leds.Select(x => x.CorsairLedColor).ToArray();
                bool setResult = CorsairLightingSDK.SetLedsColorsBufferByDeviceIndex(devices[i].DeviceIndex, leds);
                if (!setResult)
                {
                    return false;
                }
            }
            return CorsairLightingSDK.SetLedsColorsFlushBuffer();
        }

        /// <summary>
        /// Handles the error.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private bool HandleError()
        {
            CorsairError error = CorsairLightingSDK.GetLastError();
            if (error == CorsairError.ServerNotFound)
            {
                return false;
            }
            else if (error != CorsairError.Success)
            {
                throw new Exception(error + "");
            }
            return true;
        }

        public CorsairError CorsairGetLastError()
        {
            return CorsairLightingSDK.GetLastError();
        }

        /// <summary>
        /// Allows the application to set the layer priority to allow the iCue software to regain control
        /// </summary>
        /// <param name="priority"></param>
        public void SetLayerPriority(int priority)
        {
            CorsairLightingSDK.SetLayerPriority(priority);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (HasExclusiveLightingControl)
            {
                CorsairLightingSDK.ReleaseControl(CorsairAccessMode.ExclusiveLightingControl);
            }
        }
    }
}
