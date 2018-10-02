using System;
using System.Collections.Generic;

namespace WPDatabaseWork.WPClient.Model
{
    public interface IWPClient : IDisposable, IPostsRepository, ICommentRepository, ICommentMetaRepository, IPostMetaRepository
    {
        IConnection CreateConnection();
        void GetTableSchema(IConnection connection, string schema);
        void GetTableSchema(IConnection connection, string schema, string bloggId);
        IEnumerable<string> GetSchema(IConnection connection);
    }
}