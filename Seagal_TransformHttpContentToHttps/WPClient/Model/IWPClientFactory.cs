namespace Fallback_blogg.WPClient.Model
{
    public interface IWPClientFactory
    {
        IWPClient CreateClient( string connectionString);
    }
}
