using Corsair.CUE.SDK;
using IcueHelper.Models;
using System;
using System.Collections.Generic;
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
            ProtocolDetails = CUESDK.CorsairPerformProtocolHandshake();

            if (ProtocolDetails.serverProtocolVersion == 0)
            {
                if (!HandleError())
                {
                    //server not found... seep 10 seconds and try again
                    Thread.Sleep(10000);
                    initialiseSdk(exclusiveLightingControl);
                    return;
                }
            }

            if (ProtocolDetails.breakingChanges)
            {
                String sdkVersion = ProtocolDetails.sdkVersion;
                String cueVersion = ProtocolDetails.serverVersion;
                throw new Exception("Incompatible SDK (" + sdkVersion + ") and CUE " + cueVersion + " versions.");
            }

            if (exclusiveLightingControl)
            {
                CUESDK.CorsairRequestControl(CorsairAccessMode.CAM_ExclusiveLightingControl);
                HasExclusiveLightingControl = true;
            }
        }

        /// <summary>
        /// Lists the devices.
        /// </summary>
        /// <returns></returns>
        public Device[] ListDevices()
        {
            int deviceCount = CUESDK.CorsairGetDeviceCount();

            if (deviceCount > 0)
            {
                Device[] devices = new Device[deviceCount];
                for (int i = 0; i < deviceCount; i++)
                {
                    CorsairDeviceInfo deviceInfo = CUESDK.CorsairGetDeviceInfo(i);
                    CorsairLedPositions positions = GetLedPositions(i);
                    List<Led> leds = new List<Led>();
                    for (int j = 0; j < positions.pLedPosition.Length; j++)
                    {
                        leds.Add(new Led(positions.pLedPosition[j]));
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
            return CUESDK.CorsairGetLedPositionsByDeviceIndex(deviceIndex);
        }

        /// <summary>
        /// Refreshes the color of the device.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <returns></returns>
        public bool RefreshDeviceColor(Device device)
        {
            bool result = CUESDK.CorsairGetLedsColorsByDeviceIndex(device.DeviceIndex, device.Leds.Count, device.Leds.ToArray());
            device.CalculateAverageColor();
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
            bool setResult = CUESDK.CorsairSetLedsColorsBufferByDeviceIndex(device.DeviceIndex, device.Leds.Count, device.Leds.ToArray());
            if (setResult)
            {
                return CUESDK.CorsairSetLedsColorsFlushBufferAsync(null, null);
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
                bool setResult = CUESDK.CorsairSetLedsColorsBufferByDeviceIndex(devices[i].DeviceIndex, devices[i].Leds.Count, devices[i].Leds.ToArray());
                if (!setResult)
                {
                    return false;
                }
            }
            return CUESDK.CorsairSetLedsColorsFlushBuffer();
        }

        /// <summary>
        /// Handles the error.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private bool HandleError()
        {
            CorsairError error = CUESDK.CorsairGetLastError();
            if (error == CorsairError.CE_ServerNotFound)
            {
                return false;
            }
            else if (error != CorsairError.CE_Success)
            {
                throw new Exception(error + "");
            }
            return true;
        }

        public CorsairError CorsairGetLastError()
        {
            return CUESDK.CorsairGetLastError();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (HasExclusiveLightingControl)
            {
                CUESDK.CorsairReleaseControl(CorsairAccessMode.CAM_ExclusiveLightingControl);
            }
        }
    }
}
