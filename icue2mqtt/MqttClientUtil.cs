using CUESDK;
using icue2mqtt.Models;
using IcueHelper;
using IcueHelper.Models;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace icue2mqtt
{
    /// <summary>
    /// Client util providing methods for managing the connection to the MQTT broker and synchronising the icue device states
    /// </summary>
    internal class MqttClientUtil
    {
        internal static MqttClientUtil Instance { get; private set; } = new MqttClientUtil();

        private MqttClientUtil()
        {

        }

        internal const string TOPIC_CONTROL_SWITCH_CONFIG = "homeassistant/switch/icue2mtt/icue_control/config";
        internal const string TOPIC_CONTROL_SWITCH_SET = "homeassistant/switch/icue2mtt/icue_control/set";
        internal const string TOPIC_CONTROL_SWITCH_STATE = "homeassistant/switch/icue2mtt/icue_control/state";

        /// <summary>
        /// Gets or sets the icue SDK.
        /// </summary>
        /// <value>
        /// The icue SDK.
        /// </value>
        static Sdk IcueSdk { get; set; }


        /// <summary>
        /// Gets or sets the client.
        /// </summary>
        /// <value>
        /// The client.
        /// </value>
        internal IMqttClient Client { get; set; }

        private bool stopping = false;

        internal bool HasControl { get; set; } = true;

        /// <summary>
        /// Connects to the MQTT broker.
        /// </summary>
        /// <param name="reconnecting">if set to <c>true</c> [reconnecting].</param>
        internal async System.Threading.Tasks.Task ConnectToMqttBrokerAsync(bool reconnecting)
        {
            if (reconnecting)
            {
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            Logger.LogInformation(!reconnecting ? "Connecting to MQTT broker" : "Attempting to reconnect to MQTT broker");
            bool useCredentials = icue2mqttService.appProperties.mqttCredentialsUser != null && !icue2mqttService.appProperties.mqttCredentialsUser.Trim().Equals("") &&
                icue2mqttService.appProperties.mqttCredentialsPwd != null && !icue2mqttService.appProperties.mqttCredentialsPwd.Trim().Equals("");
            try
            {

                int? portInt = null;
                if (icue2mqttService.appProperties.mqttOverridePort != null)
                {
                    int portholder;
                    if (int.TryParse(icue2mqttService.appProperties.mqttOverridePort, out portholder))
                    {
                        portInt = portholder;
                    }
                }
                MqttClientOptionsBuilder messageBuilder = messageBuilder = new MqttClientOptionsBuilder()
                      .WithClientId(Guid.NewGuid().ToString())
                      .WithTcpServer(icue2mqttService.appProperties.mqttUrl, portInt)
                      .WithCleanSession();

                if (useCredentials)
                {
                    messageBuilder.WithCredentials(icue2mqttService.appProperties.mqttCredentialsUser, icue2mqttService.appProperties.mqttCredentialsPwd);
                }

                //var managedOptionsBuilder = 

                var managedOptions = new ManagedMqttClientOptionsBuilder()
                  .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                  .WithClientOptions(messageBuilder.Build())
                  .Build();

                if (Client == null)
                {
                    Client = new MqttFactory().CreateMqttClient();
                    Client.DisconnectedAsync += async e =>
                    {
                        if (!stopping)
                        {
                            _ = ConnectToMqttBrokerAsync(true);
                        }
                    };
                    Client.ApplicationMessageReceivedAsync += client_MqttMsgPublishReceived;
                }

                await Client.ConnectAsync(managedOptions.ClientOptions, new CancellationToken());
                if (Client.IsConnected)
                {
                    Logger.LogInformation("Connected to MQTT broker");
                    PublishDevices();
                }
                else
                {
                    if (!reconnecting)
                    {
                        Logger.LogInformation("Failed to connect to MQTT broker.");
                        _ = ConnectToMqttBrokerAsync(true);
                    }
                }
            }
            catch (Exception connectionEx)
            {
                if (!reconnecting)
                {
                    Logger.LogError("Failed to connect to MQTT broker.", connectionEx);
                    _ = ConnectToMqttBrokerAsync(true);
                }
            }
        }

        /// <summary>
        /// Stops the MQTT client and disposes of the  IcueSDK releasing the icue connection
        /// </summary>
        internal async System.Threading.Tasks.Task StopAsync()
        {
            stopping = true;
            if (Client != null && Client.IsConnected)
            {
                await Client.DisconnectAsync();
                Client.Dispose();
            }
            if (IcueSdk != null)
            {
                IcueSdk.Dispose();
            }
        }

        /// <summary>
        /// Publishes the icue devices to the MQTT broker and sets up the control topics.
        /// </summary>
        internal void PublishDevices()
        {
            if (Client.IsConnected)
            {
                if (IcueSdk == null)
                {
                    IcueSdk = new Sdk(false);
                }
                if (MqttIcueDeviceList.GetDevices().Length == 0)
                {
                    GetListOfMqttDevices();
                }
                for (int i = 0; i < MqttIcueDeviceList.GetDevices().Length; i++)
                {
                    MqttIcueDevice mqttIcueDevice = MqttIcueDeviceList.GetDevices()[i];
                    if (mqttIcueDevice != null)
                    {
                        Logger.LogInformation(String.Format("Publishing device {0}", mqttIcueDevice.IcueDevice.CorsairDevice.Model));
                        MqttClientSubscribeOptions subscriptions = new MqttClientSubscribeOptions();
                        List<MqttTopicFilter> topicFilters = new List<MqttTopicFilter>();
                        topicFilters.Add(new MqttTopicFilter()
                        {
                            Topic = mqttIcueDevice.CommandTopic,
                            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce
                        });
                        subscriptions.TopicFilters = topicFilters;
                        Client.SubscribeAsync(subscriptions);
                        MqttApplicationMessage publishMessage = new MqttApplicationMessage()
                        {
                            Payload = Encoding.UTF8.GetBytes(mqttIcueDevice.Discovery.ToJson()),
                            Topic = mqttIcueDevice.DiscoveryTopic,
                            Retain = true,
                            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce
                        };
                        Client.PublishAsync(publishMessage).ContinueWith(e => { SendStateUpdate(mqttIcueDevice); });
                    }
                }
                if (MqttIcueDeviceList.GetDevices().Length > 0)
                {
                    //publish the all device entity
                    MqttClientSubscribeOptions subscriptions = new MqttClientSubscribeOptions();
                    List<MqttTopicFilter> topicFilters = new List<MqttTopicFilter>();
                    topicFilters.Add(new MqttTopicFilter()
                    {
                        Topic = MqttIcueDeviceList.TOPIC_ALL_DEVICE_SET,
                        QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce
                    });
                    subscriptions.TopicFilters = topicFilters;
                    Client.SubscribeAsync(subscriptions);
                    MqttApplicationMessage publishMessage = new MqttApplicationMessage()
                    {
                        Payload = Encoding.UTF8.GetBytes(MqttIcueDeviceList.GetAllDeviceDiscovery().ToJson()),
                        Topic = MqttIcueDeviceList.TOPIC_ALL_DEVICE_CONFIG,
                        Retain = true,
                        QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce
                    };
                    Client.PublishAsync(publishMessage).ContinueWith(e =>
                    {
                        SendStateUpdate(MqttIcueDeviceList.TOPIC_ALL_DEVICE_STATE, MqttIcueDeviceList.GetAllDeviceAverageState());
                    });
                    

                    //publish the icue control switch
                    subscriptions = new MqttClientSubscribeOptions();
                    topicFilters = new List<MqttTopicFilter>();
                    topicFilters.Add(new MqttTopicFilter()
                    {
                        Topic = TOPIC_CONTROL_SWITCH_SET,
                        QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
                    });
                    subscriptions.TopicFilters = topicFilters;
                    Client.SubscribeAsync(subscriptions);
                    publishMessage = new MqttApplicationMessage()
                    {
                        Payload = Encoding.UTF8.GetBytes(new MqttIcueControlSwitchDiscovery(TOPIC_CONTROL_SWITCH_STATE, TOPIC_CONTROL_SWITCH_SET).ToJson()),
                        Topic = TOPIC_CONTROL_SWITCH_CONFIG,
                        Retain = false,
                        QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
                    };
                    Client.PublishAsync(publishMessage).ContinueWith(e =>
                    {
                        SendControlSwitchUpdate();
                    });
                    
                }
            }
            else
            {
                Logger.LogInformation("MQTT broker connection lost.");
            }
        }

        internal void GetListOfMqttDevices()
        {
            Device[] devices = IcueSdk.ListDevices(); 
            CorsairError error = IcueSdk.CorsairGetLastError();
            if (error != CorsairError.Success)
            {
                Logger.LogError("SDK error getting list of devices", new Exception(error.ToString()));
            }
            Dictionary<string, int> modelCount = new Dictionary<string, int>();
            for (int i = 0; i < devices.Length; i++)
            {
                //handle multiple devices of same model
                int modelSuffix = 0;
                if (modelCount.ContainsKey(devices[i].CorsairDevice.Model))
                {
                    modelSuffix = modelCount[devices[i].CorsairDevice.Model] + 1;
                    modelCount[devices[i].CorsairDevice.Model] = modelSuffix;
                }
                else
                {
                    modelCount.Add(devices[i].CorsairDevice.Model, modelSuffix);
                }
                MqttIcueDeviceList.AddIcueDevice(devices[i], modelSuffix);
            }
        }

        /// <summary>
        /// Handles the MqttMsgPublishReceived event of the client control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MqttMsgPublishEventArgs"/> instance containing the event data.</param>
        private Task client_MqttMsgPublishReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            string jsonMessage = Encoding.UTF8.GetString(e.ApplicationMessage.Payload, 0, e.ApplicationMessage.Payload.Length);
            Console.WriteLine(e.ApplicationMessage.Topic);
            if (e.ApplicationMessage.Topic == MqttIcueDeviceList.TOPIC_ALL_DEVICE_STATE)
            {
                SendStateUpdate(MqttIcueDeviceList.TOPIC_ALL_DEVICE_STATE, MqttIcueDeviceList.GetAllDeviceAverageState());
                return Task.CompletedTask;
            }

            if (e.ApplicationMessage.Topic == TOPIC_CONTROL_SWITCH_STATE)
            {
                SendControlSwitchUpdate();
                return Task.CompletedTask;
            }

            MqttIcueDevice mqttIcueDevice = MqttIcueDeviceList.GetDeviceByStateTopic(e.ApplicationMessage.Topic);
            if (mqttIcueDevice != null)
            {
                SendStateUpdate(mqttIcueDevice);
                return Task.CompletedTask;
            }
            bool isSetAllDevices = e.ApplicationMessage.Topic == MqttIcueDeviceList.TOPIC_ALL_DEVICE_SET;
            mqttIcueDevice = MqttIcueDeviceList.GetDeviceBySetTopic(e.ApplicationMessage.Topic);
            if (mqttIcueDevice != null || isSetAllDevices)
            {
                // The command can contain a state (ON/OFF), a color, a brightness or any combination of them
                MqttIcueDeviceState command = JsonConvert.DeserializeObject<MqttIcueDeviceState>(jsonMessage);
                if (command == null)
                {
                    return Task.CompletedTask;
                }
                ApplyCommand(mqttIcueDevice, isSetAllDevices, command);
                CorsairError error = IcueSdk.CorsairGetLastError();
                if (error != CorsairError.Success)
                {
                    Logger.LogError("SDK error applying command to device", new Exception(error.ToString()));
                }
                return Task.CompletedTask;
            }

            if (e.ApplicationMessage.Topic == TOPIC_CONTROL_SWITCH_SET)
            {
                if (jsonMessage.Equals("ON"))
                {
                    HasControl = true;
                    IcueSdk.SetLayerPriority(130);
                }
                else
                {
                    HasControl = false;
                    IcueSdk.SetLayerPriority(126);
                }
                SendControlSwitchUpdate();
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Applies a command (state, color and/or brightness) to one device or to all the devices,
        /// then publishes the new state(s).
        /// </summary>
        /// <param name="mqttIcueDevice">The device, ignored when <paramref name="isAllDevices"/> is true.</param>
        /// <param name="isAllDevices">if set to <c>true</c> the command is applied to all the devices.</param>
        /// <param name="command">The command received over MQTT.</param>
        private void ApplyCommand(MqttIcueDevice mqttIcueDevice, bool isAllDevices, MqttIcueDeviceState command)
        {
            if (isAllDevices)
            {
                MqttIcueDevice[] allDevices = MqttIcueDeviceList.GetDevices();
                foreach (MqttIcueDevice icueDevice in allDevices)
                {
                    icueDevice.ApplyCommand(IcueSdk, command);
                }
                foreach (MqttIcueDevice icueDevice in allDevices)
                {
                    SendStateUpdate(icueDevice);
                }
                SendStateUpdate(MqttIcueDeviceList.TOPIC_ALL_DEVICE_STATE, MqttIcueDeviceList.GetAllDeviceAverageState());
            }
            else
            {
                mqttIcueDevice.ApplyCommand(IcueSdk, command);
                SendStateUpdate(mqttIcueDevice);
            }
        }

        /// <summary>
        /// Handles the ConnectionClosedEvent event of the client control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
        private void client_ConnectionClosedEvent(object sender, EventArgs e)
        {
            if (stopping)
            {
                return;
            }
            Logger.LogInformation(String.Format("{0} - MQTT client closed", new DateTime()));
            _ = ConnectToMqttBrokerAsync(true);
        }

        /// <summary>
        /// Sends the state update to MQTT broker.
        /// </summary>
        /// <param name="mqttIcueDevice">The MQTT icue device.</param>
        private void SendStateUpdate(MqttIcueDevice mqttIcueDevice)
        {
            if (Client.IsConnected)
            {
                Client.PublishAsync(new MqttApplicationMessageBuilder()
                  .WithTopic(mqttIcueDevice.StateTopic)
                  .WithPayload(Encoding.UTF8.GetBytes(mqttIcueDevice.GetState().ToJson()))
                  .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                  .WithRetainFlag(true)
                  .Build());
            }
        }

        private void SendStateUpdate(string stateTopic, MqttIcueDeviceState state)
        {
            if (Client.IsConnected)
            {
                Client.PublishAsync(new MqttApplicationMessageBuilder()
                  .WithTopic(stateTopic)
                  .WithPayload(Encoding.UTF8.GetBytes(state.ToJson()))
                  .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                  .WithRetainFlag(true)
                  .Build());
            }
        }

        private void SendControlSwitchUpdate()
        {
            if (Client.IsConnected)
            {
                Client.PublishAsync(new MqttApplicationMessageBuilder()
                  .WithTopic(TOPIC_CONTROL_SWITCH_STATE)
                  .WithPayload(Encoding.UTF8.GetBytes(HasControl? "ON": "OFF"))
                  .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                  .WithRetainFlag(false)
                  .Build());
            }
        }

    }
}
