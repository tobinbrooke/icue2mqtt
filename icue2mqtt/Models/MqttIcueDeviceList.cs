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

        private static readonly List<Device> icueDevices = new List<Device>();

        internal static int LastAverageR { get; set; }

        internal static int LastAverageG { get; set; }

        internal static int LastAverageB { get; set; }

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
            string entityId = icueDevice.CorsairDevice.model.Replace(" ", "_");
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
                    if (devices[i].IcueDevice.CorsairDevice.model.Equals(mqttIcueDevice.IcueDevice.CorsairDevice.model) && suffixNumber == devices[i].SuffixNumber)
                    {
                        devices[i] = mqttIcueDevice;
                        icueDevices[i] = icueDevice;
                        break;
                    }
                }
                setTopicDeviceMap[commandTopic] = mqttIcueDevice;
                stateTopicDeviceMap[stateTopic] = mqttIcueDevice;
            }
            else
            {
                devices.Add(mqttIcueDevice);
                icueDevices.Add(icueDevice);
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

        internal static MqttIcueDeviceState GetAllDeviceAverageState()
        {
            MqttIcueDeviceState result = new MqttIcueDeviceState();
            if (devices.Count == 0)
            {
                result.State = "OFF";
            }
            else
            {
                int totalR = 0;
                int totalG = 0;
                int totalB = 0;
                foreach (MqttIcueDevice device in devices)
                {
                    totalR += device.IcueDevice.R;
                    totalG += device.IcueDevice.G;
                    totalB += device.IcueDevice.B;
                }
                LastAverageR = totalR / devices.Count;
                LastAverageG = totalG / devices.Count;
                LastAverageB  = totalB / devices.Count;
                result.State = (LastAverageR > 0 || LastAverageG > 0 || LastAverageB > 0) ? "ON" : "OFF";
                result.Color = new MqttColor(LastAverageR, LastAverageG, LastAverageB);
            }
            return result;
        }

        internal static MqttIcueDeviceDiscovery GetAllDeviceDiscovery()
        {
            return new MqttIcueDeviceDiscovery("all icue", TOPIC_ALL_DEVICE_STATE, TOPIC_ALL_DEVICE_SET, 0);
        }

        internal static void SetAllDeviceState(IcueHelper.Sdk IcueSdk, int R, int G, int B)
        {
            LastAverageR = R;
            LastAverageG = G;
            LastAverageB = B;
            IcueSdk.SetDeviceColor(icueDevices.ToArray(), R, G, B);
        }
    }
}
