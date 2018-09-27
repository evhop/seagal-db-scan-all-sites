using System;

namespace WPDatabaseWork.Model
{
    public interface ICommandContext
    {
        IServiceProvider ServiceProvider { get; }
    }
}