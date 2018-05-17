using System;
using System.Collections.Generic;
using System.Text;

namespace Migration.Analys
{
    public interface IAnalys
    {
        string Name { get; }
        void Execute(RunContext context);
    }
}
