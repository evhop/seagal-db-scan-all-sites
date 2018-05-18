using System;
using System.Collections.Generic;

namespace Seagal_TransformHttpContentToHttps.WPClient.Model
{
    public interface IWPClient : IDisposable, IPostsRepository, ICommentRepository, IPostMetaRepository, IUserRepository, IUserMetaRepository, ICommentMetaRepository
    {
        IConnection CreateConnection();
        void GetTableSchema(IConnection connection, string schema);
        IEnumerable<string> GetSchema(IConnection connection);
    }
}