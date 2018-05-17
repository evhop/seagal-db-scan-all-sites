using Newtonsoft.Json;

namespace Seagal_TransformHttpContentToHttps.View
{
    public class SettingsDb
    {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        public string Characterset { get; set; } = "utf8mb4";
    }
}