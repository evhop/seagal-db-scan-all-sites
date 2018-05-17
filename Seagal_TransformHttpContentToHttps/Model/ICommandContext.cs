using System;

namespace Seagal_TransformHttpContentToHttps.Model
{
    public interface ICommandContext
    {
        IServiceProvider ServiceProvider { get; }
    }
}