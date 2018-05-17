using Newtonsoft.Json;

namespace Seagal_TransformHttpContentToHttps.View
{
    public class DatabaseSettings
    {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("database")]
        public string Database { get; set; }

        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("characterset")]
        public string Characterset { get; set; } = "utf8mb4";

        public string BuildConnectionString() => $"Server={Host}; Database={Database};Uid={User}; Pwd={Password}; Character Set={Characterset}";
    }
}