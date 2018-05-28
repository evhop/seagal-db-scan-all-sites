﻿using System;
using System.Collections.Generic;

namespace Fallback_blogg.WPClient.Model
{
    public interface IWPClient : IDisposable, IPostsRepository, ICommentRepository, ICommentMetaRepository, IPostMetaRepository
    {
        IConnection CreateConnection();
        void GetTableSchema(IConnection connection, string schema);
        IEnumerable<string> GetSchema(IConnection connection);
    }
}