using System;
using System.Collections.Generic;

namespace Seagal_TransformHttpContentToHttps.WPClient.View
{
    public class Post
    {
        public string SchemaTable { get; set; }
        public ulong Id { get; set; }
        public string Content { get; set; }
        public string Excerpt { get; set; }
        public string ContentFiltered { get; set; }
    }
}
