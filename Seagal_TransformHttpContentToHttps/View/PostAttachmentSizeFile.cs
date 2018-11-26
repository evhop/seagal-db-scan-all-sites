namespace WPDatabaseWork.View
{
    public class PostAttachmentSizeFile
    {
        public string file { get; set; } //content reference
        public int width => 150;
        public int height => 150;
        public string mime_type { get; set; } // content format
    }
}
