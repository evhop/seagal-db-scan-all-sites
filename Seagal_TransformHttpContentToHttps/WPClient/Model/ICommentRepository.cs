using WPDatabaseWork.WPClient.View;
using System.Collections.Generic;

namespace WPDatabaseWork.WPClient.Model
{
    public interface ICommentRepository
	{
        void UpdateComments(IConnection connection, string replaceFrom, string replaceTo);
        void UpdateComments(IConnection connection, IEnumerable<Post> comments);
        IEnumerable<Post> GetComments(IConnection connection, string likeSearch);
    }
}