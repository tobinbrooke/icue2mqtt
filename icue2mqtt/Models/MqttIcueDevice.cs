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
        /// Gets the colour (at full intensity) and brightness of the light.
        /// They are kept when the light is turned off, so they can be restored when turning it back on.
        /// </summary>
        /// <value>
        /// The level.
        /// </value>
        internal MqttLightLevel Level { get; private set; }

        /// <summary>
        /// Gets the suffix number used for tracking if more than one device of model exists.
        /// </summary>
        /// <value>
        /// The suffix number.
        /// </value>
        internal int SuffixNumber { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttIcueDevice"/> class.
        /// </summary>
        /// <param name="icueDevice">The icue device.</param>
        /// <param name="stateTopic">The state topic.</param>
        /// <param name="commandTopic">The command topic.</param>
        /// <param name="discoveryTopic">The discovery topic.</param>
        internal MqttIcueDevice(Device icueDevice, string stateTopic, string commandTopic, string discoveryTopic, int suffixNumber)
        {
            this.IcueDevice = icueDevice;
            this.StateTopic = stateTopic;
            this.CommandTopic = commandTopic;
            this.DiscoveryTopic = discoveryTopic;
            this.Level = new MqttLightLevel(icueDevice.R, icueDevice.G, icueDevice.B);
            this.SuffixNumber = suffixNumber;
            this.Discovery = new MqttIcueDeviceDiscovery(icueDevice.CorsairDevice.Model, stateTopic, commandTopic, suffixNumber);

        }

        /// <summary>
        /// Applies a command received over MQTT: turns the device off, or on with the requested
        /// colour and/or brightness (the ones not specified are left unchanged).
        /// </summary>
        /// <param name="sdk">The icue SDK.</param>
        /// <param name="command">The command.</param>
        internal void ApplyCommand(IcueHelper.Sdk sdk, MqttIcueDeviceState command)
        {
            if (command.IsOffCommand())
            {
                sdk.SetDeviceColor(IcueDevice, 0, 0, 0);
                return;
            }
            Level.Update(command.Color, command.Brightness);
            MqttColor output = Level.GetOutputColor();
            sdk.SetDeviceColor(IcueDevice, output.R, output.G, output.B);
        }

        /// <summary>
        /// Gets the state object for sending as JSON over MQTT.
        /// </summary>
        /// <returns></returns>
        internal MqttIcueDeviceState GetState()
        {
            bool isOn = IcueDevice.R > 0 || IcueDevice.G > 0 || IcueDevice.B > 0;
            return isOn
                ? MqttIcueDeviceState.On(Level.GetBaseColor(), Level.Brightness)
                : MqttIcueDeviceState.Off();
        }
    }
}
