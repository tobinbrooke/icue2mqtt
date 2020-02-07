using icue2mqtt.Models;
using IcueHelper;
using IcueHelper.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace icue2mqtt
{
    /// <summary>
    /// Windows service class for getting the list of icue devices and managing the state over MQTT
    /// </summary>
    /// <seealso cref="System.ServiceProcess.ServiceBase" />
    class icue2mqttService : ServiceBase
    {
        private static string _logFileLocation;

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
        private static MqttClient Client { get; set; }

        private static Task clientTask;

        /// <summary>
        /// Logs the specified log message.
        /// </summary>
        /// <param name="logMessage">The log message.</param>
        private void Log(string logMessage)
        {
            if (_logFileLocation == null || _logFileLocation.Trim().Equals(""))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_logFileLocation));
            File.AppendAllText(_logFileLocation, DateTime.UtcNow.ToString() + " : " + logMessage + Environment.NewLine);
        }

        /// <summary>
        /// Called when service starts and is debuggable.
        /// </summary>
        /// <param name="args">The startup arguments.</param>
        public void OnStartPublic(string[] args)
        {
            _logFileLocation = Properties.Resources.logPath;

            Log("Starting");
            
            if (Properties.Resources.mqttUrl == null || Properties.Resources.mqttUrl.Trim().Equals(""))
            {
                Log("No MQTT broker URL. Stopping");
                base.Stop();
                return;
            }

            //run MQTT client in seperate thread to allow service to start whilst waiting for icue to start
            clientTask = new Task(() => connectMqttAndDevices());
            clientTask.Start();
        }

        private void connectMqttAndDevices()
        {
            Log("Connecting to MQTT broker");
            Client = new MqttClient(Properties.Resources.mqttUrl);
            Client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            string clientId = Guid.NewGuid().ToString();
            bool useCredentials = Properties.Resources.mqttCredentialsUser != null && !Properties.Resources.mqttCredentialsUser.Trim().Equals("") &&
                Properties.Resources.mqttCredentialsPwd != null && !Properties.Resources.mqttCredentialsPwd.Trim().Equals("");
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
                Log("Connected to MQTT broker");
                IcueSdk = new Sdk(false);
                Device[] devices = IcueSdk.ListDevices();
                for (int i = 0; i < devices.Length; i++)
                {
                    MqttIcueDevice mqttIcueDevice = MqttIcueDeviceList.AddIcueDevice(devices[i]);
                    if (mqttIcueDevice != null)
                    {
                        Log(String.Format("Publishing device {0}", mqttIcueDevice.IcueDevice.CorsairDevice.model));
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
                Log("Failed to connect to MQTT broker. Stopping service");
                base.Stop();
            }
        }

        /// <summary>
        /// Handles the MqttMsgPublishReceived event of the client control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MqttMsgPublishEventArgs"/> instance containing the event data.</param>
        static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
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
                    }
                    else
                    {
                        mqttIcueDevice.SetOffState();
                        IcueSdk.SetDeviceColor(mqttIcueDevice.IcueDevice, 0, 0, 0);
                    }
                    return;
                }
                else
                {
                    IcueSdk.SetDeviceColor(mqttIcueDevice.IcueDevice, state.Color.R, state.Color.G, state.Color.B);
                }
                return;
            }
        }

        /// <summary>
        /// Sends the state update to MQTT broker.
        /// </summary>
        /// <param name="mqttIcueDevice">The MQTT icue device.</param>
        private static void SendStateUpdate(MqttIcueDevice mqttIcueDevice)
        {
            Client.Publish(
                  mqttIcueDevice.StateTopic,
                  Encoding.UTF8.GetBytes(mqttIcueDevice.GetState().ToJson()),
                  MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                  false);
        }

        protected override void OnStart(string[] args)
        {
            OnStartPublic(args);
            base.OnStart(args);
        }

        /// <summary>
        /// Called when service is stopped to be debuggable.
        /// </summary>
        public void OnStopPublic()
        {
            Log("Stopping");
            if (Client != null && Client.IsConnected)
            {
                Client.Disconnect();
            }
            if (IcueSdk != null)
            {
                IcueSdk.Dispose();
            }
            if (clientTask != null)
            {
                clientTask.Dispose();
            }
        }

        protected override void OnStop()
        {
            OnStopPublic();
            base.OnStop();
        }

        protected override void OnPause()
        {
            Log("Pausing");
            base.OnPause();
        }
    }
}
