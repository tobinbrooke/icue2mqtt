using System;
using System.Diagnostics;
using System.IO;

namespace icue2mqtt
{
    internal static class Logger
    {

        internal static string _logFileLocation { get; set; }

        /// <summary>
        /// The logger
        /// </summary>
        internal static EventLog logger { get; set; }

        /// <summary>
        /// Setups the logging.
        /// </summary>
        internal static void SetupLogging()
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
        internal static void LogInformation(string logMessage)
        {
            try
            {
                if (logger != null)
                {
                    logger.WriteEntry(logMessage, EventLogEntryType.Information);
                }
                else
                {
                    LogToFile(logMessage, null);
                }
            }
            catch (Exception ex)
            {
                LogToFile(logMessage, ex);
            }
        }

        /// <summary>
        /// Logs the error.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="ex">The ex.</param>
        internal static void LogError(string message, Exception ex)
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
                    LogToFile(message, ex);
                }
            }
            catch (Exception exception)
            {
                LogToFile(message, exception);
            }
        }

        /// <summary>
        /// Logs to file.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="ex">The ex.</param>
        private static void LogToFile(string message, Exception ex)
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
