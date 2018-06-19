﻿using System.Collections.Generic;
using Fallback_blogg.WPClient.View;

namespace Fallback_blogg.WPClient.Model
{
	public interface IPostsRepository
	{
		void UpdatePosts( IConnection connection, IEnumerable<Post> contents, string colum);
        void UpdatePosts(IConnection connection, string replaceFrom, string replaceTo);
        IEnumerable<Post> GetPosts(IConnection connection, string colum);
        IEnumerable<Post> GetPosts(IConnection connection, string colum, string likeSearch);
        void CreateSqlUpdatePostsfile(IConnection connection, IEnumerable<Post> posts, string colum, string path, string time);
    }
}