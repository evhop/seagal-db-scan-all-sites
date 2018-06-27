using Fallback_blogg.Core;

namespace Fallback_blogg.Analys
{
    public interface ISourceRewrites
    {
        string Name { get; }
        void ExecuteAllHttpLinks(Context context, string time);
        void ExecuteUpdateDomain(Context context);
        void ExecuteGetDomain(Context context, string time);
        void WriteUrlToFile(string path);    
    }
}
