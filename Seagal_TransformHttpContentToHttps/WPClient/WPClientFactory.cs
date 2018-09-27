using WPDatabaseWork.WPClient.Model;

namespace WPDatabaseWork.WPClient
{
    public class WPClientFactory : IWPClientFactory
    {
        public IWPClient CreateClient( string connectionString) => new WPClient( connectionString);
    }
}
