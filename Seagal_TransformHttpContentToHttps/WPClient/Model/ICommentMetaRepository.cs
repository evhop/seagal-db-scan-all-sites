using System.Collections.Generic;
using Seagal_TransformHttpContentToHttps.WPClient.View;

namespace Seagal_TransformHttpContentToHttps.WPClient.Model
{
    public interface ICommentMetaRepository
    {
        IEnumerable<Meta> GetCommentMeta(IConnection connection);
        void UpdateCommentMetas(IConnection connection, IEnumerable<Meta> commentMetas);
    }
}
