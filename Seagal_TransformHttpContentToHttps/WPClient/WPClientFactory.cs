using Fallback_blogg.WPClient.Model;

namespace Fallback_blogg.WPClient
{
    public class WPClientFactory : IWPClientFactory
    {
        public IWPClient CreateClient( string connectionString) => new WPClient( connectionString);
    }
}
