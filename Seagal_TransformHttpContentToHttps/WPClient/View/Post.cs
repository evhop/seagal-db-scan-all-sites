using System;
using System.Collections.Generic;

namespace Fallback_blogg.WPClient.View
{
    public class Post
    {
        public string SchemaTable { get; set; }
        public ulong Id { get; set; }
        public string Content { get; set; }
        public string OldContent { get; set; }
    }
}
