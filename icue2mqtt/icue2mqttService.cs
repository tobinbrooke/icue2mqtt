﻿using Newtonsoft.Json;
using System;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace icue2mqtt
{
    /// <summary>
    /// Windows service class for getting the list of icue devices and managing the state over MQTT
    /// </summary>
    /// <seealso cref="System.ServiceProcess.ServiceBase" />
    class icue2mqttService : ServiceBase
    {
        public static AppProperties appProperties { get; set; }

        /// <summary>
        /// The client task
        /// </summary>
        private static Task clientTask;

        /// <summary>
        /// Called when service starts and is debuggable.
        /// </summary>
        /// <param name="args">The startup arguments.</param>
        public void OnStartPublic(string[] args)
        {
            LoadAppProperties();
            Logger.SetupLogging();
            Logger.LogInformation("Starting");

            if (appProperties == null || appProperties.mqttUrl == null || appProperties.mqttUrl.Trim().Equals(""))
            {
                Logger.LogInformation("No MQTT broker URL. Stopping");
                base.Stop();
                return;
            }

            //run MQTT client in seperate thread to allow service to start whilst waiting for icue to start
            clientTask = new Task(() => { _ = MqttClientUtil.Instance.ConnectToMqttBrokerAsync(false); });
            clientTask.Start();
        }

        public void LoadAppProperties()
        {
            string jsonString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "properties.json"));
            appProperties = JsonConvert.DeserializeObject<AppProperties>(jsonString);
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
        public async Task OnStopPublicAsync()
        {
            Logger.LogInformation("Stopping");
            await MqttClientUtil.Instance.StopAsync();
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
            OnStopPublicAsync().ContinueWith(e =>
            {
                base.OnStop();
            });
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Pause command is sent to the service by the Service Control Manager (SCM). Specifies actions to take when a service pauses.
        /// </summary>
        protected override void OnPause()
        {
            Logger.LogInformation("Pausing");
            base.OnPause();
        }

    }
}
