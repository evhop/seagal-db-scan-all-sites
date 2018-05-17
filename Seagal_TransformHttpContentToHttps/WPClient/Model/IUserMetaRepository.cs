using System.Collections.Generic;
using Seagal_TransformHttpContentToHttps.WPClient.View;

namespace Seagal_TransformHttpContentToHttps.WPClient.Model
{
    public interface IUserMetaRepository
    {
        IEnumerable<Meta> GetUserMeta(IConnection connection);
        void UpdateUserMetas(IConnection connection, IEnumerable<Meta> userMetas);
    }
}