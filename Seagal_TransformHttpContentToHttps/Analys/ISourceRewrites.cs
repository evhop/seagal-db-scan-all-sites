using Fallback_blogg.Core;

namespace Fallback_blogg.Analys
{
    public interface ISourceRewrites
    {
        string Name { get; }
        void Execute(Context context, string time);
    }
}
