using System;

namespace icue2mqtt.Models
{
    /// <summary>
    /// Tracks the colour and the brightness of a light separately, the way Home Assistant models a light.
    /// The colour sent to the iCUE device is the "full intensity" colour scaled by the brightness.
    /// Keeping both values lets us restore them when the light is turned back on.
    /// </summary>
    internal class MqttLightLevel
    {
        /// <summary>
        /// Maximum value of a colour channel and of the brightness.
        /// </summary>
        internal const int MaxValue = 255;

        /// <summary>
        /// Gets the red channel of the full intensity colour (at least one channel is 255).
        /// </summary>
        internal int BaseR { get; private set; } = MaxValue;

        /// <summary>
        /// Gets the green channel of the full intensity colour.
        /// </summary>
        internal int BaseG { get; private set; } = MaxValue;

        /// <summary>
        /// Gets the blue channel of the full intensity colour.
        /// </summary>
        internal int BaseB { get; private set; } = MaxValue;

        /// <summary>
        /// Gets the brightness from 1 to 255.
        /// </summary>
        internal int Brightness { get; private set; } = MaxValue;

        /// <summary>
        /// Initializes a new instance with a white light at full brightness.
        /// </summary>
        internal MqttLightLevel()
        {
        }

        /// <summary>
        /// Initializes a new instance from a colour currently displayed by a device.
        /// A black colour keeps the defaults (white, full brightness) so the light can still be turned on.
        /// </summary>
        internal MqttLightLevel(int r, int g, int b)
        {
            SetFromOutput(r, g, b);
        }

        /// <summary>
        /// Computes value * numerator / denominator, rounded to the nearest integer.
        /// </summary>
        internal static int Scale(int value, int numerator, int denominator)
        {
            return (value * numerator + denominator / 2) / denominator;
        }

        /// <summary>
        /// Splits a colour as displayed by a device into a full intensity colour and a brightness.
        /// Does nothing for black, so the previous colour and brightness are kept.
        /// </summary>
        internal void SetFromOutput(int r, int g, int b)
        {
            int max = Math.Max(r, Math.Max(g, b));
            if (max <= 0)
            {
                return;
            }
            SetBase(r, g, b, max);
            Brightness = Math.Min(max, MaxValue);
        }

        /// <summary>
        /// Updates the level from a command received over MQTT.
        /// </summary>
        /// <param name="color">The full intensity colour, or null to keep the current one.</param>
        /// <param name="brightness">The brightness, or null to keep the current one.</param>
        internal void Update(MqttColor color, int? brightness)
        {
            if (color != null)
            {
                int max = Math.Max(color.R, Math.Max(color.G, color.B));
                if (max > 0)
                {
                    SetBase(color.R, color.G, color.B, max);
                    // A colour darker than full intensity without any brightness is a raw RGB value
                    // (not sent by Home Assistant): its intensity becomes the brightness.
                    if (!brightness.HasValue && max < MaxValue)
                    {
                        Brightness = max;
                    }
                }
            }
            if (brightness.HasValue)
            {
                Brightness = Math.Min(Math.Max(brightness.Value, 1), MaxValue);
            }
        }

        /// <summary>
        /// Gets the full intensity colour (what Home Assistant displays as the light colour).
        /// </summary>
        internal MqttColor GetBaseColor()
        {
            return new MqttColor(BaseR, BaseG, BaseB);
        }

        /// <summary>
        /// Gets the colour to send to the device: the full intensity colour scaled by the brightness.
        /// </summary>
        internal MqttColor GetOutputColor()
        {
            return new MqttColor(
                Scale(BaseR, Brightness, MaxValue),
                Scale(BaseG, Brightness, MaxValue),
                Scale(BaseB, Brightness, MaxValue));
        }

        private void SetBase(int r, int g, int b, int max)
        {
            BaseR = Scale(Math.Max(r, 0), MaxValue, max);
            BaseG = Scale(Math.Max(g, 0), MaxValue, max);
            BaseB = Scale(Math.Max(b, 0), MaxValue, max);
        }
    }
}
