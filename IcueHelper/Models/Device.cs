using CUESDK;
using System.Collections.Generic;

namespace IcueHelper.Models
{
    /// <summary>
    /// class representing a icue device and associated LEDs from the SDK
    /// </summary>
    public class Device
    {
        /// <summary>
        /// Gets the corsair device from the SDK wrapper.
        /// </summary>
        /// <value>
        /// The corsair device.
        /// </value>
        public CorsairDeviceInfo CorsairDevice { get; internal set; }

        /// <summary>
        /// Gets the index of the device.
        /// </summary>
        /// <value>
        /// The index of the device.
        /// </value>
        public int DeviceIndex { get; internal set; }

        /// <summary>
        /// The LEDs belonging to the device
        /// </summary>
        public List<Led> Leds = new List<Led>();

        /// <summary>
        /// Gets the r color value average from all LEDs on the device.
        /// </summary>
        /// <value>
        /// The r.
        /// </value>
        public int R { get; private set; } = 0;

        /// <summary>
        /// Gets the g color value average from all LEDs on the device.
        /// </summary>
        /// <value>
        /// The g.
        /// </value>
        public int G { get; private set; } = 0;

        /// <summary>
        /// Gets the b color value average from all LEDs on the device.
        /// </summary>
        /// <value>
        /// The b.
        /// </value>
        public int B { get; private set; } = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Device"/> class.
        /// </summary>
        /// <param name="corsairDevice">The corsair device.</param>
        /// <param name="deviceIndex">Index of the device.</param>
        /// <param name="leds">The leds.</param>
        internal Device(CorsairDeviceInfo corsairDevice, int deviceIndex, List<Led> leds)
        {
            this.CorsairDevice = corsairDevice;
            this.DeviceIndex = deviceIndex;
            this.Leds = leds;
        }

        /// <summary>
        /// Sets the color.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        public void SetColor(int r, int g, int b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            foreach (Led led in Leds)
            {
                led.R = r;
                led.G = g;
                led.B = b;
            }
        }

        /// <summary>
        /// Calculates the average color.
        /// </summary>
        public void CalculateAverageColor()
        {
            if (Leds == null || Leds.Count == 0)
            {
                this.R = 0;
                this.G = 0;
                this.B = 0;
                return;
            }
            int r = 0;
            int g = 0;
            int b = 0;
            foreach (Led led in Leds)
            {
                r += led.R;
                g += led.G;
                b += led.B;
            }
            this.R = r / Leds.Count;
            this.G = g / Leds.Count;
            this.B = b / Leds.Count;
        }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return this.CorsairDevice.DeviceId != null? CorsairDevice.Model: base.ToString();
        }
    }
}
