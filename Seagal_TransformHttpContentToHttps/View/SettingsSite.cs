namespace WPDatabaseWork.View
{
    public class SettingsSite
    {
        private long _azureBlobContainer;

        internal string DestinationAzureBlob { get; set; }
        public string DestinationAzureBlobKey { get; set; }

        internal string GetAzureBlobUrl
        {
            get
            {
                return $"https://{DestinationAzureBlob}.blob.core.windows.net/";
            }
        }

        internal string GetAzureBlobAccount
        {
            get
            {
                return DestinationAzureBlob;
            }
        }
        internal string GetBlobContainer()
        {
            return $"{_azureBlobContainer:D3}";
        }

    }
}