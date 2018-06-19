using Fallback_blogg.View;
using Fallback_blogg.WPClient.Model;
using System.Collections.Generic;

namespace Fallback_blogg.Model
{
    public interface IContext: ICommandContext
    {
        Options Options { get; }
        Settings Settings { get; }
    }
}
