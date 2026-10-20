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
        internal const string TOPIC_ALL_DEVICE_CONFIG = "homeassistant/light/icue2mtt/all_icue/config";
        internal const string TOPIC_ALL_DEVICE_SET = "homeassistant/light/icue2mtt/all_icue/set";
        internal const string TOPIC_ALL_DEVICE_STATE = "homeassistant/light/icue2mtt/all_icue/state";

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
        internal static MqttIcueDevice AddIcueDevice(Device icueDevice, int suffixNumber)
        {
            if (icueDevice == null)
            {
                return null;
            }
            string entityId = icueDevice.CorsairDevice.Model.Replace(" ", "_").Replace(":", "_");
            if (suffixNumber > 0)
            {
                entityId += "_" + suffixNumber;
            }
            string stateTopic = String.Format("homeassistant/light/icue2mtt/{0}/state", entityId);
            string commandTopic = String.Format("homeassistant/light/icue2mtt/{0}/set", entityId);
            string discoveryTopic = String.Format("homeassistant/light/icue2mtt/{0}/config", entityId);
            MqttIcueDevice mqttIcueDevice = new MqttIcueDevice(icueDevice, stateTopic, commandTopic, discoveryTopic, suffixNumber);
            if (stateTopicDeviceMap.ContainsKey(stateTopic))
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i].IcueDevice.CorsairDevice.Model.Equals(mqttIcueDevice.IcueDevice.CorsairDevice.Model) && suffixNumber == devices[i].SuffixNumber)
                    {
                        devices[i] = mqttIcueDevice;
                        break;
                    }
                }
                setTopicDeviceMap[commandTopic] = mqttIcueDevice;
                stateTopicDeviceMap[stateTopic] = mqttIcueDevice;
            }
            else
            {
                devices.Add(mqttIcueDevice);
                setTopicDeviceMap.Add(commandTopic, mqttIcueDevice);
                stateTopicDeviceMap.Add(stateTopic, mqttIcueDevice);
            }
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

        /// <summary>
        /// Gets the state of the "all icue" entity: the average of the colors displayed by all the devices,
        /// split into a full intensity color and a brightness.
        /// </summary>
        internal static MqttIcueDeviceState GetAllDeviceAverageState()
        {
            if (devices.Count == 0)
            {
                return MqttIcueDeviceState.Off();
            }
            int totalR = 0;
            int totalG = 0;
            int totalB = 0;
            foreach (MqttIcueDevice device in devices)
            {
                totalR += device.IcueDevice.R;
                totalG += device.IcueDevice.G;
                totalB += device.IcueDevice.B;
            }
            return MqttIcueDeviceState.FromOutput(totalR / devices.Count, totalG / devices.Count, totalB / devices.Count);
        }

        internal static MqttIcueDeviceDiscovery GetAllDeviceDiscovery()
        {
            return new MqttIcueDeviceDiscovery("all icue", TOPIC_ALL_DEVICE_STATE, TOPIC_ALL_DEVICE_SET, 0);
        }
    }
}
