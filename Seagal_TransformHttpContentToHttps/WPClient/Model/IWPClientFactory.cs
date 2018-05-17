namespace Seagal_TransformHttpContentToHttps.WPClient.Model
{
    public interface IWPClientFactory
    {
        IWPClient CreateClient( string connectionString, ITableNameGenerator generator );
    }
}
