using Corsair.CUE.SDK;
using icue2mqtt.Models;
using IcueHelper;
using IcueHelper.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
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

        /// <summary>
        /// The client task
        /// </summary>
        private static Task clientTask;

        /// <summary>
        /// The logger
        /// </summary>
        private static EventLog logger;

        /// <summary>
        /// Called when service starts and is debuggable.
        /// </summary>
        /// <param name="args">The startup arguments.</param>
        public void OnStartPublic(string[] args)
        {
            LogInformation("Starting");

            _logFileLocation = Properties.Resources.logPath;

            setupLogging();

            if (Properties.Resources.mqttUrl == null || Properties.Resources.mqttUrl.Trim().Equals(""))
            {
                LogInformation("No MQTT broker URL. Stopping");
                base.Stop();
                return;
            }

            //run MQTT client in seperate thread to allow service to start whilst waiting for icue to start
            clientTask = new Task(() => connectMqttAndDevices());
            clientTask.Start();
        }

        /// <summary>
        /// Connects the MQTT and devices.
        /// </summary>
        private void connectMqttAndDevices()
        {
            LogInformation("Connecting to MQTT broker");
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
            Client.ConnectionClosed += client_ConnectionClosedEvent;

            if (Client.IsConnected)
            {
                LogInformation("Connected to MQTT broker");
                IcueSdk = new Sdk(false);
                Device[] devices = IcueSdk.ListDevices();
                for (int i = 0; i < devices.Length; i++)
                {
                    MqttIcueDevice mqttIcueDevice = MqttIcueDeviceList.AddIcueDevice(devices[i]);
                    if (mqttIcueDevice != null)
                    {
                        LogInformation(String.Format("Publishing device {0}", mqttIcueDevice.IcueDevice.CorsairDevice.model));
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
                LogInformation("Failed to connect to MQTT broker. Stopping service");
                base.Stop();
            }
        }

        /// <summary>
        /// Handles the ConnectionClosedEvent event of the client control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
        static void client_ConnectionClosedEvent(object sender, EventArgs e)
        {
            LogInformation(String.Format("{0} - MQTT client closed", new DateTime()));
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
                        CorsairError error = IcueSdk.CorsairGetLastError();
                        if (error != CorsairError.CE_Success)
                        {
                            LogError("SDK error setting device to ON", new Exception(error.ToString()));
                        }
                    }
                    else
                    {
                        mqttIcueDevice.SetOffState();
                        IcueSdk.SetDeviceColor(mqttIcueDevice.IcueDevice, 0, 0, 0);
                        CorsairError error = IcueSdk.CorsairGetLastError();
                        if (error != CorsairError.CE_Success)
                        {
                            LogError("SDK error setting device to OFF", new Exception(error.ToString()));
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
                        LogError("SDK error setting device colo", new Exception(error.ToString()));
                    }
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

        /// <summary>
        /// When implemented in a derived class, executes when a Start command is sent to the service by the Service Control Manager (SCM) or when the operating system starts (for a service that starts automatically). Specifies actions to take when the service starts.
        /// </summary>
        /// <param name="args">Data passed by the start command.</param>
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
            LogInformation("Stopping");
            if (Client != null && Client.IsConnected)
            {
                Client.Disconnect();
            }
            if (IcueSdk != null)
            {
                IcueSdk.Dispose();
            }
            if (clientTask != null && clientTask.IsCompleted)
            {
                clientTask.Dispose();
            }
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Stop command is sent to the service by the Service Control Manager (SCM). Specifies actions to take when a service stops running.
        /// </summary>
        protected override void OnStop()
        {
            OnStopPublic();
            base.OnStop();
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Pause command is sent to the service by the Service Control Manager (SCM). Specifies actions to take when a service pauses.
        /// </summary>
        protected override void OnPause()
        {
            LogInformation("Pausing");
            base.OnPause();
        }

        /// <summary>
        /// Setups the logging.
        /// </summary>
        private static void setupLogging()
        {

            // Create the source, if it does not already exist.
            if (!EventLog.SourceExists("icue2mqtt"))
            {
                //An event log source should not be created and immediately used.
                //There is a latency time to enable the source, it should be created
                //prior to executing the application that uses the source.
                //Execute this sample a second time to use the new source.
                EventLog.CreateEventSource("icue2mqtt", "icue2mqttLog");
            }

            // Create an EventLog instance and assign its source.
            logger = new EventLog();
            logger.Source = "icue2mqtt";
        }

        /// <summary>
        /// Logs the specified log message.
        /// </summary>
        /// <param name="logMessage">The log message.</param>
        private static void LogInformation(string logMessage)
        {
            try
            {
                if (logger != null)
                {
                    logger.WriteEntry(logMessage, EventLogEntryType.Information);
                }
                else
                {
                    logToFile(logMessage, null);
                }
            }
            catch(Exception ex)
            {
                logToFile(logMessage, ex);
            }
        }

        /// <summary>
        /// Logs the error.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="ex">The ex.</param>
        private static void LogError(string message, Exception ex)
        {
            try
            {
                if (logger != null)
                {
                    logger.WriteEntry(
                        string.Format("{0}: {1} {2}{3}", message, ex.Message, Environment.NewLine, ex.StackTrace), 
                        EventLogEntryType.Error);
                }
                else
                {
                    logToFile(message, ex);
                }
            }
            catch (Exception exception)
            {
                logToFile(message, exception);
            }
        }

        /// <summary>
        /// Logs to file.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="ex">The ex.</param>
        private static void logToFile(string message, Exception ex)
        {

            if (_logFileLocation == null || _logFileLocation.Trim().Equals(""))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_logFileLocation));
            if (ex != null)
            {
                File.AppendAllText(_logFileLocation,
                    string.Format("{0} : {1} - {2}{3}", DateTime.UtcNow.ToString(), message, ex.Message, Environment.NewLine));
            }
            else
            {
                File.AppendAllText(_logFileLocation,
                    string.Format("{0} : {1}{2}", DateTime.UtcNow.ToString(), message, Environment.NewLine));
            }
        }
    }
}
