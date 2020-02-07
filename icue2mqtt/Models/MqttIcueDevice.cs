using IcueHelper.Models;

namespace icue2mqtt.Models
{
    /// <summary>
    ///  Class representing a MQTT icue device to be controlled
    /// </summary>
    internal class MqttIcueDevice
    {
        /// <summary>
        /// Gets the associated icue device.
        /// </summary>
        /// <value>
        /// The icue device.
        /// </value>
        internal Device IcueDevice { get; private set; }

        /// <summary>
        /// Gets the state topic.
        /// </summary>
        /// <value>
        /// The state topic.
        /// </value>
        internal string StateTopic { get; private set; }

        /// <summary>
        /// Gets the command topic.
        /// </summary>
        /// <value>
        /// The command topic.
        /// </value>
        internal string CommandTopic { get; private set; }

        /// <summary>
        /// Gets the discovery topic.
        /// </summary>
        /// <value>
        /// The discovery topic.
        /// </value>
        internal string DiscoveryTopic { get; private set; }

        /// <summary>
        /// Gets the discovery object to send to discovery topic.
        /// </summary>
        /// <value>
        /// The discovery.
        /// </value>
        internal MqttIcueDeviceDiscovery Discovery { get; private set; }

        /// <summary>
        /// Gets the last known r color value.
        /// Used for restoring color when turning the device back on
        /// </summary>
        /// <value>
        /// The last r.
        /// </value>
        internal int LastR { get; private set; }

        /// <summary>
        /// Gets the last known g color value.
        /// Used for restoring color when turning the device back on
        /// </summary>
        /// <value>
        /// The last g.
        /// </value>
        internal int LastG { get; private set; }

        /// <summary>
        /// Gets the last known r color value.
        /// Used for restoring color when turning the device back on
        /// </summary>
        /// <value>
        /// The last b.
        /// </value>
        internal int LastB { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttIcueDevice"/> class.
        /// </summary>
        /// <param name="icueDevice">The icue device.</param>
        /// <param name="stateTopic">The state topic.</param>
        /// <param name="commandTopic">The command topic.</param>
        /// <param name="discoveryTopic">The discovery topic.</param>
        internal MqttIcueDevice(Device icueDevice, string stateTopic, string commandTopic, string discoveryTopic)
        {
            this.IcueDevice = icueDevice;
            this.StateTopic = stateTopic;
            this.CommandTopic = commandTopic;
            this.DiscoveryTopic = discoveryTopic;
            this.LastR = icueDevice.R;
            this.LastG = icueDevice.G;
            this.LastB = icueDevice.B;
            this.Discovery = new MqttIcueDeviceDiscovery(icueDevice.CorsairDevice.model, stateTopic, commandTopic);

        }

        /// <summary>
        /// Sets the last color values when turning LEDS off.
        /// </summary>
        internal void SetOffState()
        {
            this.LastR = IcueDevice.R;
            this.LastG = IcueDevice.G;
            this.LastB = IcueDevice.B;
        }

        /// <summary>
        /// Gets the state object for sending as JSON over MQTT.
        /// </summary>
        /// <returns></returns>
        internal MqttIcueDeviceState GetState()
        {
            return new MqttIcueDeviceState(IcueDevice);
        }
    }
}
