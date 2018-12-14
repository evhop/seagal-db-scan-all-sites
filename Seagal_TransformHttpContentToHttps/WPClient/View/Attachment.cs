using System;
using System.Collections.Generic;
using System.Text;

namespace WPDatabaseWork.WPClient.View
{
    public class Attachment
    {
        public ulong Id { get; set; }
        public ulong Author { get; set; }
        public DateTime Date { get; set; }
        public DateTime DateGMT { get; set; }
        public string Content { get; set; }
        public string Title { get; set; }
        public string Status { get; set; } = "inherit";
        public string CommentStatus { get; set; } = "open";
        public string PingStatus { get; set; } = "open";
        public string Name { get; set; }
        public ulong ParentId { get; set; }
        public string Guid { get; set; }
        public string Type { get; set; } = "attachment";
        public string MimeType { get; set; }

        public List<Meta> Metas { get; set; }
    }
}
