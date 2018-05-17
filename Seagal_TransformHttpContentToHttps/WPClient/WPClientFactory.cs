using Seagal_TransformHttpContentToHttps.WPClient.Model;

namespace Seagal_TransformHttpContentToHttps.WPClient
{
    public class WPClientFactory : IWPClientFactory
    {
        public IWPClient CreateClient( string connectionString, ITableNameGenerator generator ) => new WPClient( connectionString, generator );
    }
}
