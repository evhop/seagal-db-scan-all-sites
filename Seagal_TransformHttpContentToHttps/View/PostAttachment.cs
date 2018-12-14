using System;
using System.Collections.Generic;
using System.Text;

namespace WPDatabaseWork.View
{
    public class PostAttachment
    {
        //_wp_attachment_metadata
        public int width { get; set; }
        public int height { get; set; }
        //+ _wp_attached_file
        public string file { get; set; }

        public List<PostAttachmentSize> sizes { get; set; }
        public PostAttachmentImageMeta image_meta { get; set; }
    }
}
