using Fallback_blogg.WPClient.View;
using System.Collections.Generic;

namespace Fallback_blogg.WPClient.Model
{
    public interface ICommentRepository
	{
        void UpdateComments(IConnection connection, IEnumerable<Comment> comments);
        IEnumerable<Comment> GetComments(IConnection connection, string likeSearch);
    }
}