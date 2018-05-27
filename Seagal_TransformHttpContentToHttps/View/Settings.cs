using Newtonsoft.Json;
using System.Collections.Generic;


namespace Fallback_blogg.View
{
    public class Settings
    {

        [JsonProperty("tablePrefix")]
        public string TablePrefix { get; set; }

        [JsonProperty("db")]
        public virtual IEnumerable<SettingsDb> Db { get; set; }
        public SettingsDb DestinationDb { get; internal set; }
        public string DestinationBuildConnectionString() => $"Server={DestinationDb.Host}; Uid={DestinationDb.User}; Pwd={DestinationDb.Password}; Character Set={DestinationDb.Characterset};SslMode=none";
    }
}
