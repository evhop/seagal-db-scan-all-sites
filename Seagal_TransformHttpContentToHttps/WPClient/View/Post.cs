using System;
using System.Collections.Generic;

namespace WPDatabaseWork.WPClient.View
{
    public class Post
    {
        public string SchemaTable { get; set; }
        public ulong Id { get; set; }
        public string Date { get; set; }
        public string Content { get; set; }
        public string OldContent { get; set; }
        public string Guid { get; set; }
    }
}
