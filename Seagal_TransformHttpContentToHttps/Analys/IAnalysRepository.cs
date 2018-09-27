using System.Collections.Generic;

namespace WPDatabaseWork.Analys
{
    public interface IAnalysRepository
    {
        IReadOnlyList<ISourceRewrites> Analysis { get; }
        ISourceRewrites GetAnalys(string name);
    }
}
