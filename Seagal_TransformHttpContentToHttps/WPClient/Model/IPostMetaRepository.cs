using System.Collections.Generic;
using WPDatabaseWork.WPClient.View;

namespace WPDatabaseWork.WPClient.Model
{
    public interface IPostMetaRepository
    {
        void InsertPostMetas(IConnection connection, IEnumerable<Meta> metas);
        void CreateSqlInsertPostMetasfile(IConnection connection, IEnumerable<Meta> metas, string path, string time);
        IEnumerable<Meta> GetPostMeta( IConnection connection, string metaKey);
        void UpdatePostMetas( IConnection connection, IEnumerable<Meta> postMetas );
        void UpdatePostMetas(IConnection connection, string replaceFrom, string replaceTo);
    }
}
