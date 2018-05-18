using Seagal_TransformHttpContentToHttps.View;
using Seagal_TransformHttpContentToHttps.WPClient.Model;
using System.Collections.Generic;

namespace Seagal_TransformHttpContentToHttps.Model
{
    public interface IContext: ICommandContext
    {
        Settings Settings { get; }
    }
}
