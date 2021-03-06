﻿using Newtonsoft.Json;
using System.Collections.Generic;


namespace WPDatabaseWork.View
{
    public class Settings
    {

        [JsonProperty("tablePrefix")]
        public string TablePrefix { get; set; }

        [JsonProperty("rewriteUrlToHttps")]
        public IEnumerable<string> RewriteUrlToHttps { get; set; }

        [JsonProperty("db")]
        public virtual IEnumerable<SettingsDb> Db { get; set; }

        public SettingsDb DestinationDb { get; internal set; }
        public SettingsSite DestinationSite { get; internal set; }
        public string DestinationBuildConnectionString() => $"Server={DestinationDb.Host}; Uid={DestinationDb.User}; Pwd={DestinationDb.Password}; Character Set={DestinationDb.Characterset};SslMode=none";
    }
}
