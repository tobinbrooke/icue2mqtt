using Newtonsoft.Json;
using System.Collections.Generic;

namespace icue2mqtt.Models
{
    internal class MqttIcueDeviceDiscovery : JsonConvertableBase
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

        [JsonProperty("supported_color_modes")]
        public List<string> SupportedColorModes { get; private set; }

        [JsonProperty("optimistic")]
        public bool Optimisitic { get; private set; } = false;

        [JsonProperty("qos")]
        public int Qos { get; private set; } = 0;

        internal MqttIcueDeviceDiscovery(string name, string stateTopic, string commandTopic, int suffixNumber)
        {
            this.Name = name;
            this.UniqueId = name.Replace(" ", "_");
            if (suffixNumber > 0)
            {
                this.Name += " " + suffixNumber;
                this.UniqueId += "_" + suffixNumber;
            }
            this.StateTopic = stateTopic;
            this.CommandTopic = commandTopic;
            this.SupportedColorModes = new List<string> { "rgb" };
        }

        internal MqttIcueDeviceDiscovery(string name, string stateTopic, string commandTopic, int suffixNumber, bool isSwitch)
        {
            this.Name = name;
            this.UniqueId = name.Replace(" ", "_");
            if (suffixNumber > 0)
            {
                this.Name += " " + suffixNumber;
                this.UniqueId += "_" + suffixNumber;
            }
            this.StateTopic = stateTopic;
            this.CommandTopic = commandTopic;
            this.SupportedColorModes = isSwitch ? new List<string>() : new List<string> { "rgb" };
        }
    }
}
