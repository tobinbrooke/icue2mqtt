using Corsair.CUE.SDK;
using icue2mqtt.Models;
using IcueHelper;
using IcueHelper.Models;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

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
            bool useCredentials = Properties.Resources.mqttCredentialsUser != null && !Properties.Resources.mqttCredentialsUser.Trim().Equals("") &&
                Properties.Resources.mqttCredentialsPwd != null && !Properties.Resources.mqttCredentialsPwd.Trim().Equals("");
            try
            {

                MqttClientOptionsBuilder messageBuilder = messageBuilder = new MqttClientOptionsBuilder()
                      .WithClientId(Guid.NewGuid().ToString())
                      .WithTcpServer(Properties.Resources.mqttUrl)
                      .WithCleanSession();
                if (useCredentials)
                {
                    messageBuilder.WithCredentials(Properties.Resources.mqttCredentialsUser, Properties.Resources.mqttCredentialsPwd);
                }

                var managedOptions = new ManagedMqttClientOptionsBuilder()
                  .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                  .WithClientOptions(messageBuilder.Build())
                  .Build();

                if (Client == null)
                {
                    Client = new MqttFactory().CreateMqttClient();
                    Client.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(e =>
                    {
                        if (!stopping)
                        {
                            _ = ConnectToMqttBrokerAsync(true);
                        }
                    });
                    Client.UseApplicationMessageReceivedHandler(client_MqttMsgPublishReceived);
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
                        Logger.LogInformation(String.Format("Publishing device {0}", mqttIcueDevice.IcueDevice.CorsairDevice.model));
                        MqttClientSubscribeOptions subscriptions = new MqttClientSubscribeOptions();
                        List<TopicFilter> topicFilters = new List<TopicFilter>();
                        topicFilters.Add(new TopicFilter()
                        {
                            Topic = mqttIcueDevice.StateTopic,
                            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce
                        });
                        topicFilters.Add(new TopicFilter()
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
                    List<TopicFilter> topicFilters = new List<TopicFilter>();
                    topicFilters.Add(new TopicFilter()
                    {
                        Topic = MqttIcueDeviceList.TOPIC_ALL_DEVICE_STATE,
                        QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce
                    });
                    topicFilters.Add(new TopicFilter()
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
                    topicFilters = new List<TopicFilter>();
                    topicFilters.Add(new TopicFilter()
                    {
                        Topic = TOPIC_CONTROL_SWITCH_SET,
                        QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
                    });
                    topicFilters.Add(new TopicFilter()
                    {
                        Topic = TOPIC_CONTROL_SWITCH_STATE,
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
            if (error != CorsairError.CE_Success)
            {
                Logger.LogError("SDK error getting list of devices", new Exception(error.ToString()));
            }
            Dictionary<string, int> modelCount = new Dictionary<string, int>();
            for (int i = 0; i < devices.Length; i++)
            {
                //handle multiple devices of same model
                int modelSuffix = 0;
                if (modelCount.ContainsKey(devices[i].CorsairDevice.model))
                {
                    modelSuffix = modelCount[devices[i].CorsairDevice.model] + 1;
                    modelCount[devices[i].CorsairDevice.model] = modelSuffix;
                }
                else
                {
                    modelCount.Add(devices[i].CorsairDevice.model, modelSuffix);
                }
                MqttIcueDeviceList.AddIcueDevice(devices[i], modelSuffix);
            }
        }

        /// <summary>
        /// Handles the MqttMsgPublishReceived event of the client control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MqttMsgPublishEventArgs"/> instance containing the event data.</param>
        private void client_MqttMsgPublishReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            string jsonMessage = Encoding.UTF8.GetString(e.ApplicationMessage.Payload, 0, e.ApplicationMessage.Payload.Length);

            if (e.ApplicationMessage.Topic == MqttIcueDeviceList.TOPIC_ALL_DEVICE_STATE)
            {
                SendStateUpdate(MqttIcueDeviceList.TOPIC_ALL_DEVICE_STATE, MqttIcueDeviceList.GetAllDeviceAverageState());
                return;
            }

            if (e.ApplicationMessage.Topic == TOPIC_CONTROL_SWITCH_STATE)
            {
                SendControlSwitchUpdate();
                return;
            }

            MqttIcueDevice mqttIcueDevice = MqttIcueDeviceList.GetDeviceByStateTopic(e.ApplicationMessage.Topic);
            if (mqttIcueDevice != null)
            {
                SendStateUpdate(mqttIcueDevice);
                return;
            }
            bool isSetAllDevices = e.ApplicationMessage.Topic == MqttIcueDeviceList.TOPIC_ALL_DEVICE_SET;
            mqttIcueDevice = MqttIcueDeviceList.GetDeviceBySetTopic(e.ApplicationMessage.Topic);
            if (mqttIcueDevice != null || isSetAllDevices)
            {
                MqttIcueDeviceState state = JsonConvert.DeserializeObject<MqttIcueDeviceState>(jsonMessage);
                if (state.Color == null)
                {
                    if (state.State.Equals("ON"))
                    {
                        SetState(mqttIcueDevice, isSetAllDevices);
                        CorsairError error = IcueSdk.CorsairGetLastError();
                        if (error != CorsairError.CE_Success)
                        {
                            Logger.LogError("SDK error setting device to ON", new Exception(error.ToString()));
                        }
                    }
                    else
                    {
                        mqttIcueDevice.SetOffState();
                        SetState(mqttIcueDevice, isSetAllDevices, 0, 0, 0);
                        CorsairError error = IcueSdk.CorsairGetLastError();
                        if (error != CorsairError.CE_Success)
                        {
                            Logger.LogError("SDK error setting device to OFF", new Exception(error.ToString()));
                        }
                    }
                    return;
                }
                else
                {
                    SetState(mqttIcueDevice, isSetAllDevices, state.Color.R, state.Color.G, state.Color.B);
                    CorsairError error = IcueSdk.CorsairGetLastError();
                    if (error != CorsairError.CE_Success)
                    {
                        Logger.LogError("SDK error setting device color", new Exception(error.ToString()));
                    }
                }
                return;
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
                return;
            }
        }

        private void SetState(MqttIcueDevice mqttIcueDevice, bool isAllDevices, int R, int G, int B)
        {
            if (isAllDevices)
            {
                MqttIcueDeviceList.SetAllDeviceState(IcueSdk, R, G, B);
                foreach(MqttIcueDevice icueDevice in MqttIcueDeviceList.GetDevices())
                {
                    SendStateUpdate(icueDevice);
                }
            }
            else
            {
                IcueSdk.SetDeviceColor(mqttIcueDevice.IcueDevice, R, G, B);
            }
        }

        private void SetState(MqttIcueDevice mqttIcueDevice, bool isAllDevices)
        {
            if (isAllDevices)
            {
                SetState(mqttIcueDevice, isAllDevices, MqttIcueDeviceList.LastAverageR, MqttIcueDeviceList.LastAverageG, MqttIcueDeviceList.LastAverageB);
            }
            else
            {
                SetState(mqttIcueDevice, isAllDevices, mqttIcueDevice.LastR, mqttIcueDevice.LastG, mqttIcueDevice.LastB);
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
                  .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
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
                  .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
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
                  .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                  .WithRetainFlag(true)
                  .Build());
            }
        }

    }
}
