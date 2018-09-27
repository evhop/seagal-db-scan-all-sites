using System.Collections.Generic;
using WPDatabaseWork.WPClient.View;

namespace WPDatabaseWork.WPClient.Model
{
    public interface IPostMetaRepository
    {
        IEnumerable<Meta> GetPostMeta( IConnection connection, string likeSearch);
        void UpdatePostMetas( IConnection connection, IEnumerable<Meta> postMetas );
        void UpdatePostMetas(IConnection connection, string replaceFrom, string replaceTo);
    }
}
