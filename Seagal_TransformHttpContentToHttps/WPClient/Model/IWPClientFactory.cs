namespace WPDatabaseWork.WPClient.Model
{
    public interface IWPClientFactory
    {
        IWPClient CreateClient( string connectionString);
    }
}
