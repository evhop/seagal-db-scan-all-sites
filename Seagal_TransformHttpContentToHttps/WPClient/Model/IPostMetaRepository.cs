using System.Collections.Generic;
using Fallback_blogg.WPClient.View;

namespace Fallback_blogg.WPClient.Model
{
    public interface IPostMetaRepository
    {
        IEnumerable<Meta> GetPostMeta( IConnection connection, string likeSearch);
        void UpdatePostMetas( IConnection connection, IEnumerable<Meta> postMetas );
   }
}
