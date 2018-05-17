using System;
using System.Collections.Generic;
using System.Text;

namespace Migration.Analys
{
    public interface IAnalysRepository
    {
        IReadOnlyList<IAnalys> Analysis { get; }
        IAnalys GetAnalys(string name);
    }
}
