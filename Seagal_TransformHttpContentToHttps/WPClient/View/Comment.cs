using System;
using System.Collections.Generic;

namespace Seagal_TransformHttpContentToHttps.WPClient.View
{
    public class Comment
    {
        public string SchemaTable { get; set; }
        public ulong Id { get; set; }
        public string AuthorUrl { get; set; }
        public string Content { get; set; }
    }
}
