using System.Collections.Generic;

namespace Fallback_blogg.Analys
{
    public interface IAnalysRepository
    {
        IReadOnlyList<ISourceRewrites> Analysis { get; }
        ISourceRewrites GetAnalys(string name);
    }
}
