using WPDatabaseWork.Core;

namespace WPDatabaseWork.Analys
{
    public interface ISourceRewrites
    {
        string Name { get; }
        void Execute(Context context, string time);
        void ExecuteUpdate(Context context);
        void WriteUrlToFile(string path);    
    }
}
