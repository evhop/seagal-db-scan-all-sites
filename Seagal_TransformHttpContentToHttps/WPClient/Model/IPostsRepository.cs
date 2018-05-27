using System.Collections.Generic;
using Fallback_blogg.WPClient.View;

namespace Fallback_blogg.WPClient.Model
{
	public interface IPostsRepository
	{
		void UpdatePosts( IEnumerable<Post> contents );
		void UpdatePosts( IConnection connection, IEnumerable<Post> contents );
        IEnumerable<Post> GetPosts(IConnection connection);
        void CreateSqlUpdatePostsfile(IConnection connection, IEnumerable<Post> posts);
    }
}