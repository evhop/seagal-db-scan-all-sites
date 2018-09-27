namespace WPDatabaseWork.Model
{
    public interface ILogTransformer
    {
        string TransformLog(string logText);
    }
}