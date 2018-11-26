using System;
using System.Collections.Generic;
using System.Text;

namespace WPDatabaseWork.View
{
    public class PostMetaWindowStorage
    {
        //windows_azure_storage_info
        public string container { get; set; }
        public string blob { get; set; } //FilePath
        public string url { get; set; }
        public List<string> thumbnails { get; set; }
    }
}
