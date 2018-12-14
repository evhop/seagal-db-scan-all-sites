using System.Collections.Generic;
using WPDatabaseWork.WPClient.View;

namespace WPDatabaseWork.WPClient.Model
{
	public interface IPostsRepository
	{
        long CountPosts(IConnection connection, string postTable, string searchValue);
        void InsertPosts(IConnection connection, IEnumerable<Attachment> posts);
        void UpdatePosts( IConnection connection, IEnumerable<Post> contents, string colum);
        void UpdatePosts(IConnection connection, string replaceFrom, string replaceTo);
        void UpdateParentIds(IConnection connection, IEnumerable<Post> contents);
        IEnumerable<Post> GetPosts(IConnection connection, string colum);
        IEnumerable<Post> GetPostsRegexp(IConnection connection, string colum, string regexp);
        IEnumerable<Post> GetPosts(IConnection connection, string colum, string likeSearch, int limit);
        IEnumerable<Post> GetAttachments(IConnection connection);
        IEnumerable<Post> GetPostsWithImagesAndNoAttachments(IConnection connection, string likeSearch);

        IEnumerable<Post> GetRecipeLinks(IConnection connection);
        void CreateSqlUpdatePostsfile(IConnection connection, IEnumerable<Post> posts, string colum, string path, string time);
        void CreateSqlReplaceUpdatePostsfile(IConnection connection, IEnumerable<Post> posts, string colum, string path, string time);
    }
}