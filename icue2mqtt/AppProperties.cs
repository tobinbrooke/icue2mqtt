using System;
using System.Collections.Generic;
using System.Text;

namespace icue2mqtt
{
    public class AppProperties
    {
        public string logPath { get; set; }
        public string mqttCredentialsPwd { get; set; }
        public string mqttCredentialsUser { get; set; }
        public string mqttUrl { get; set; }
    }
}
