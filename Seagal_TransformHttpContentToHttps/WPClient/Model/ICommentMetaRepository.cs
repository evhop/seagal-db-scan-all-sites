using System.Collections.Generic;
using WPDatabaseWork.WPClient.View;

namespace WPDatabaseWork.WPClient.Model
{
    public interface ICommentMetaRepository
    {
        IEnumerable<Meta> GetCommentMeta(IConnection connection, string likeSearch);
        void UpdateCommentMetas(IConnection connection, IEnumerable<Meta> commentMetas);
        void UpdateCommentMetas(IConnection connection, string replaceFrom, string replaceTo);
    }
}
