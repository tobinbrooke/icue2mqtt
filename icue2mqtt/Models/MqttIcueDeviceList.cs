using IcueHelper.Models;
using System;
using System.Collections.Generic;

namespace icue2mqtt.Models
{
    /// <summary>
    /// Class to track MQTT controlled icue devices
    /// </summary>
    internal static class MqttIcueDeviceList
    {
        /// <summary>
        /// The devices
        /// </summary>
        private static readonly List<MqttIcueDevice> devices = new List<MqttIcueDevice>();

        /// <summary>
        /// The set topic device map linking set topics to devices
        /// </summary>
        private static readonly Dictionary<string, MqttIcueDevice> setTopicDeviceMap = new Dictionary<string, MqttIcueDevice>();

        /// <summary>
        /// The state topic device map linking state topics to devices
        /// </summary>
        private static readonly Dictionary<string, MqttIcueDevice> stateTopicDeviceMap = new Dictionary<string, MqttIcueDevice>();

        /// <summary>
        /// Adds the icue device. Sets the topics and create instances of MqttIcueDevice
        /// </summary>
        /// <param name="icueDevice">The icue device.</param>
        /// <returns>the newly created instance of MqttIcueDevice</returns>
        internal static MqttIcueDevice AddIcueDevice(Device icueDevice)
        {
            if (icueDevice == null)
            {
                return null;
            }
            string stateTopic = String.Format("homeassistant/light/icue2mtt/{0}/state", icueDevice.CorsairDevice.model.Replace(" ", "_"));
            string commandTopic = String.Format("homeassistant/light/icue2mtt/{0}/set", icueDevice.CorsairDevice.model.Replace(" ", "_"));
            string discoveryTopic = String.Format("homeassistant/light/icue2mtt/{0}/config", icueDevice.CorsairDevice.model.Replace(" ", "_"));
            MqttIcueDevice mqttIcueDevice = new MqttIcueDevice(icueDevice, stateTopic, commandTopic, discoveryTopic);
            devices.Add(mqttIcueDevice);
            setTopicDeviceMap.Add(commandTopic, mqttIcueDevice);
            stateTopicDeviceMap.Add(stateTopic, mqttIcueDevice);
            return mqttIcueDevice;
        }

        /// <summary>
        /// Gets the device by state topic.
        /// </summary>
        /// <param name="topic">The topic.</param>
        /// <returns></returns>
        internal static MqttIcueDevice GetDeviceByStateTopic(string topic)
        {
            if (stateTopicDeviceMap.ContainsKey(topic))
            {
                return stateTopicDeviceMap[topic];
            }
            return null;
        }

        /// <summary>
        /// Gets the device by set topic.
        /// </summary>
        /// <param name="topic">The topic.</param>
        /// <returns></returns>
        internal static MqttIcueDevice GetDeviceBySetTopic(string topic)
        {
            if (setTopicDeviceMap.ContainsKey(topic))
            {
                return setTopicDeviceMap[topic];
            }
            return null;
        }

        /// <summary>
        /// Gets the device by topic.
        /// </summary>
        /// <param name="topic">The topic.</param>
        /// <returns></returns>
        internal static MqttIcueDevice GetDeviceByTopic(string topic)
        {
            MqttIcueDevice device = GetDeviceByStateTopic(topic);
            if (device != null)
            {
                return device;
            }
            return GetDeviceBySetTopic(topic);
        }

        /// <summary>
        /// Gets the devices.
        /// </summary>
        /// <returns></returns>
        internal static MqttIcueDevice[] GetDevices()
        {
            return devices.ToArray();
        }
    }
}
