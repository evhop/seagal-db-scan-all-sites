using System.Collections.Generic;
using Seagal_TransformHttpContentToHttps.WPClient.View;

namespace Seagal_TransformHttpContentToHttps.WPClient.Model
{
	public interface IPostsRepository
	{
		void UpdatePosts( IEnumerable<Post> contents );
		void UpdatePosts( IConnection connection, IEnumerable<Post> contents );
        IEnumerable<Post> GetPosts();
    }
}