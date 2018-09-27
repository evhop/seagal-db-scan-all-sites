using WPDatabaseWork.View;
using WPDatabaseWork.WPClient.Model;
using System.Collections.Generic;

namespace WPDatabaseWork.Model
{
    public interface IContext: ICommandContext
    {
        Options Options { get; }
        Settings Settings { get; }
    }
}
