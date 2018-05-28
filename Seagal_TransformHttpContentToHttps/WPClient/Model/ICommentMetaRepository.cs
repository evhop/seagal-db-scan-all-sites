using System.Collections.Generic;
using Fallback_blogg.WPClient.View;

namespace Fallback_blogg.WPClient.Model
{
    public interface ICommentMetaRepository
    {
        IEnumerable<Meta> GetCommentMeta(IConnection connection, string likeSearch);
        void UpdateCommentMetas(IConnection connection, IEnumerable<Meta> commentMetas);
    }
}
