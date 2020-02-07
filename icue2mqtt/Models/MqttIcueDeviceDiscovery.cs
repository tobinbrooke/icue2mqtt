using Newtonsoft.Json;

namespace icue2mqtt.Models
{
    internal class MqttIcueDeviceDiscovery: JsonConvertableBase
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("unique_id")]
        public string UniqueId { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; private set; } = "mqtt_json";

        [JsonProperty("state_topic")]
        public string StateTopic { get; private set; }

        [JsonProperty("command_topic")]
        public string CommandTopic { get; private set; }

        [JsonProperty("schema")]
        public string Schema { get; private set; } = "json";

        [JsonProperty("rgb")]
        public bool Rgb { get; private set; } = true;

        [JsonProperty("optimistic")]
        public bool Optimisitic { get; private set; } = false;

        [JsonProperty("qos")]
        public int Qos { get; private set; } = 0;

        internal MqttIcueDeviceDiscovery(string name, string stateTopic, string commandTopic)
        {
            this.Name = name;
            this.UniqueId = name.Replace(" ", "_");
            this.StateTopic = stateTopic;
            this.CommandTopic = commandTopic;
        }

    }
}
