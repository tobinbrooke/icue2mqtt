using Newtonsoft.Json;
using System;

namespace icue2mqtt.Models
{
    /// <summary>
    /// class representing the state of a MQTT controlled icue device.
    /// Used both to publish the state and to read the commands sent by Home Assistant.
    /// </summary>
    /// <seealso cref="icue2mqtt.Models.JsonConvertableBase" />
    internal class MqttIcueDeviceState : JsonConvertableBase
    {

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>
        /// The state of either "ON" or "OFF.
        /// </value>
        [JsonProperty("state")]
        public String State { get; set; }

        /// <summary>
        /// Gets or sets the color at full intensity (the brightness is carried separately).
        /// </summary>
        /// <value>
        /// The color.
        /// </value>
        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
        public MqttColor Color { get; set; }

        /// <summary>
        /// Gets or sets the brightness from 0 to 255.
        /// </summary>
        /// <value>
        /// The brightness, or null when not specified.
        /// </value>
        [JsonProperty("brightness", NullValueHandling = NullValueHandling.Ignore)]
        public int? Brightness { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttIcueDeviceState"/> class.
        /// </summary>
        public MqttIcueDeviceState()
        {
        }

        /// <summary>
        /// Creates an "OFF" state.
        /// </summary>
        internal static MqttIcueDeviceState Off()
        {
            return new MqttIcueDeviceState { State = "OFF" };
        }

        /// <summary>
        /// Creates an "ON" state.
        /// </summary>
        /// <param name="color">The color at full intensity.</param>
        /// <param name="brightness">The brightness from 1 to 255.</param>
        internal static MqttIcueDeviceState On(MqttColor color, int brightness)
        {
            return new MqttIcueDeviceState { State = "ON", Color = color, Brightness = brightness };
        }

        /// <summary>
        /// Creates a state from a color as displayed by a device,
        /// splitting it into a full intensity color and a brightness.
        /// </summary>
        internal static MqttIcueDeviceState FromOutput(int r, int g, int b)
        {
            int max = Math.Max(r, Math.Max(g, b));
            if (max <= 0)
            {
                return Off();
            }
            MqttColor color = new MqttColor(
                MqttLightLevel.Scale(r, MqttLightLevel.MaxValue, max),
                MqttLightLevel.Scale(g, MqttLightLevel.MaxValue, max),
                MqttLightLevel.Scale(b, MqttLightLevel.MaxValue, max));
            return On(color, Math.Min(max, MqttLightLevel.MaxValue));
        }

        /// <summary>
        /// Determines whether this command, received over MQTT, asks to turn the light off.
        /// </summary>
        internal bool IsOffCommand()
        {
            if (String.Equals(State, "OFF", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (Brightness.HasValue)
            {
                return Brightness.Value <= 0;
            }
            // a black color with no brightness is the same as turning the light off
            return Color != null && Color.R <= 0 && Color.G <= 0 && Color.B <= 0;
        }
    }
}
