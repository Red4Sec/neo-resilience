using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NeoStatsPlugin.Extensions
{
    public static class JsonExtensions
    {
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings()
        {
            Formatting = Formatting.None,
        };

        /// <summary>
        /// Static constructor
        /// </summary>
        static JsonExtensions()
        {
            _settings.Converters.Add(new StringEnumConverter() { });
        }

        /// <summary>
        /// Convert to json
        /// </summary>
        public static string ToJson(this object obj) => JsonConvert.SerializeObject(obj, _settings);
    }
}
