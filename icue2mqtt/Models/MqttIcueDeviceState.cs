using IcueHelper.Models;
using Newtonsoft.Json;
using System;

namespace icue2mqtt.Models
{
    /// <summary>
    /// class representing the state of a MQTT controlled icue device
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
        /// Gets or sets the color.
        /// </summary>
        /// <value>
        /// The color.
        /// </value>
        [JsonProperty("color")]
        public MqttColor Color { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttIcueDeviceState"/> class.
        /// </summary>
        public MqttIcueDeviceState()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttIcueDeviceState"/> class.
        /// </summary>
        /// <param name="device">The device.</param>
        public MqttIcueDeviceState(Device device)
        {
            State = (device.R > 0 || device.G > 0 || device.B > 0) ? "ON" : "OFF";
            Color = new MqttColor(device.R, device.G, device.B);
        }
    }
}
