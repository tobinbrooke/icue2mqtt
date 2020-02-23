using Newtonsoft.Json;

namespace icue2mqtt.Models
{
    internal class MqttIcueControlSwitchDiscovery : JsonConvertableBase
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("unique_id")]
        public string UniqueId { get; set; }

        [JsonProperty("state_topic")]
        public string StateTopic { get; private set; }

        [JsonProperty("command_topic")]
        public string CommandTopic { get; private set; }

        internal MqttIcueControlSwitchDiscovery(string stateTopic, string commandTopic)
        {
            this.Name = "iCue Control";
            this.UniqueId = "icue_control";
            this.StateTopic = stateTopic;
            this.CommandTopic = commandTopic;
        }
    }
}
