using Seagal_TransformHttpContentToHttps.Core;

namespace Seagal_TransformHttpContentToHttps.Analys
{
    public interface ISourceRewrites
    {
        string Name { get; }
        void Execute(Context context);
    }
}
