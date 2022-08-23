namespace icue2mqtt
{
    public class AppProperties
    {
        public string logPath { get; set; }
        public string mqttCredentialsPwd { get; set; }
        public string mqttCredentialsUser { get; set; }
        public string mqttUrl { get; set; }
        public string mqttOverridePort { get; set; }
    }
}
