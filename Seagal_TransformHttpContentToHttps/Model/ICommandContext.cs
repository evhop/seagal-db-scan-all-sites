using System;

namespace Fallback_blogg.Model
{
    public interface ICommandContext
    {
        IServiceProvider ServiceProvider { get; }
    }
}