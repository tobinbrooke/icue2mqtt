using Newtonsoft.Json;

namespace icue2mqtt.Models
{
    /// <summary>
    /// Base class for converting derived objects to JSON
    /// </summary>
    internal class JsonConvertableBase
    {
        /// <summary>
        /// Converts object to json string.
        /// </summary>
        /// <returns>string JSON representation</returns>
        internal string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
