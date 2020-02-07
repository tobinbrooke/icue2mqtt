using Newtonsoft.Json;

namespace icue2mqtt.Models
{
    /// <summary>
    /// Class representing the color values of a icue device controlled over MQTT
    /// </summary>
    internal class MqttColor
    {

        /// <summary>
        /// Gets or sets the r of RGB color.
        /// </summary>
        /// <value>
        /// The r color.
        /// </value>
        [JsonProperty("r")]
        internal int R { get; set; }

        /// <summary>
        /// Gets or sets the g of RGB color.
        /// </summary>
        /// <value>
        /// The g color.
        /// </value>
        [JsonProperty("g")]
        internal int G { get; set; }

        /// <summary>
        /// Gets or sets the b of RGB color.
        /// </summary>
        /// <value>
        /// The b color.
        /// </value>
        [JsonProperty("b")]
        internal int B { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttColor"/> class.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="g">The g.</param>
        /// <param name="b">The b.</param>
        public MqttColor(int r, int g, int b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }
    }
}
