using System.Collections.Generic;

namespace Seagal_TransformHttpContentToHttps.Analys
{
    public interface IAnalysRepository
    {
        IReadOnlyList<ISourceRewrites> Analysis { get; }
        ISourceRewrites GetAnalys(string name);
    }
}
