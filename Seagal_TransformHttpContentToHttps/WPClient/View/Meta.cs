using System;
using System.Collections.Generic;
using System.Text;

namespace WPDatabaseWork.WPClient.View
{
    public class Meta
    {
        public string SchemaTable { get; set; }
        public ulong MetaId { get; set; }
        public ulong PostId { get; set; }
        public string MetaKey { get; set; }

        public string MetaValue { get; set; }
    }
}
