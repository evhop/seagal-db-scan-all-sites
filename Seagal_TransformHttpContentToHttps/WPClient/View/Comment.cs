using System;
using System.Collections.Generic;
using System.Text;

namespace WPDatabaseWork.WPClient.View
{
    public class Comment
    {
        public string SchemaTable { get; set; }
        public ulong Id { get; set; }
        public string Content { get; set; }
    }
}
