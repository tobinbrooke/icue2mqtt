using Corsair.CUE.SDK;
using icue2mqtt.Models;
using IcueHelper;
using IcueHelper.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Exceptions;
using uPLibrary.Networking.M2Mqtt.Messages;

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
        private MqttClient Client { get; set; }

        private string clientId;
        private bool stopping = false;

        /// <summary>
        /// Connects to the MQTT broker.
        /// </summary>
        /// <param name="reconnecting">if set to <c>true</c> [reconnecting].</param>
        internal void ConnectToMqttBroker(bool reconnecting)
        {
            if (reconnecting)
            {
                Thread.Sleep(10000);
            }
            Logger.LogInformation(!reconnecting ? "Connecting to MQTT broker" : "Attempting to reconnect to MQTT broker");
            Client = new MqttClient(Properties.Resources.mqttUrl);
            Client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            Client.ConnectionClosed += client_ConnectionClosedEvent;
            clientId = Guid.NewGuid().ToString();
            bool useCredentials = Properties.Resources.mqttCredentialsUser != null && !Properties.Resources.mqttCredentialsUser.Trim().Equals("") &&
                Properties.Resources.mqttCredentialsPwd != null && !Properties.Resources.mqttCredentialsPwd.Trim().Equals("");
            try
            {
                if (useCredentials)
                {
                    Client.Connect(clientId, Properties.Resources.mqttCredentialsUser, Properties.Resources.mqttCredentialsPwd);
                }
                else
                {
                    Client.Connect(clientId);
                }
                if (Client.IsConnected)
                {
                    if (reconnecting)
                    {
                        Thread.Sleep(15000);
                    }
                    Logger.LogInformation("Connected to MQTT broker");
                    PublishDevices();
                }
                else
                {
                    Logger.LogInformation("Failed to connect to MQTT broker.");
                    ConnectToMqttBroker(true);
                }
            }
            catch(MqttConnectionException connectionEx)
            {
                Logger.LogError("Failed to connect to MQTT broker.", connectionEx);
                ConnectToMqttBroker(true);
            }
        }

        /// <summary>
        /// Stops the MQTT client and disposes of the  IcueSDK releasing the icue connection
        /// </summary>
        internal void Stop()
        {
            stopping = true;
            if (Client != null && Client.IsConnected)
            {
                Client.Disconnect();
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
                        Client.Publish(
                            mqttIcueDevice.DiscoveryTopic,
                            Encoding.UTF8.GetBytes(mqttIcueDevice.Discovery.ToJson()),
                            MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                            false);
                        Client.Subscribe(new string[] { mqttIcueDevice.StateTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                        Client.Subscribe(new string[] { mqttIcueDevice.CommandTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                        SendStateUpdate(mqttIcueDevice);
                    }
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
                MqttIcueDevice mqttIcueDevice = MqttIcueDeviceList.AddIcueDevice(devices[i], modelSuffix);
            }
        }

        /// <summary>
        /// Handles the MqttMsgPublishReceived event of the client control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MqttMsgPublishEventArgs"/> instance containing the event data.</param>
        private void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string jsonMessage = Encoding.UTF8.GetString(e.Message, 0, e.Message.Length);
            MqttIcueDevice mqttIcueDevice = MqttIcueDeviceList.GetDeviceByStateTopic(e.Topic);
            if (mqttIcueDevice != null)
            {
                SendStateUpdate(mqttIcueDevice);
                return;
            }
            mqttIcueDevice = MqttIcueDeviceList.GetDeviceBySetTopic(e.Topic);
            if (mqttIcueDevice != null)
            {
                MqttIcueDeviceState state = JsonConvert.DeserializeObject<MqttIcueDeviceState>(jsonMessage);
                if (state.Color == null)
                {
                    if (state.State.Equals("ON"))
                    {
                        IcueSdk.SetDeviceColor(mqttIcueDevice.IcueDevice, mqttIcueDevice.LastR, mqttIcueDevice.LastG, mqttIcueDevice.LastB);
                        CorsairError error = IcueSdk.CorsairGetLastError();
                        if (error != CorsairError.CE_Success)
                        {
                            Logger.LogError("SDK error setting device to ON", new Exception(error.ToString()));
                        }
                    }
                    else
                    {
                        mqttIcueDevice.SetOffState();
                        IcueSdk.SetDeviceColor(mqttIcueDevice.IcueDevice, 0, 0, 0);
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
                    IcueSdk.SetDeviceColor(mqttIcueDevice.IcueDevice, state.Color.R, state.Color.G, state.Color.B);
                    CorsairError error = IcueSdk.CorsairGetLastError();
                    if (error != CorsairError.CE_Success)
                    {
                        Logger.LogError("SDK error setting device colo", new Exception(error.ToString()));
                    }
                }
                return;
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
            ConnectToMqttBroker(true);
        }

        /// <summary>
        /// Sends the state update to MQTT broker.
        /// </summary>
        /// <param name="mqttIcueDevice">The MQTT icue device.</param>
        private void SendStateUpdate(MqttIcueDevice mqttIcueDevice)
        {
            if (Client.IsConnected)
            {
                Client.Publish(
                  mqttIcueDevice.StateTopic,
                  Encoding.UTF8.GetBytes(mqttIcueDevice.GetState().ToJson()),
                  MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                  false);
            }
        }

    }
}
