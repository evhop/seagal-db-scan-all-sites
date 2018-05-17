using System.Collections.Generic;
using Seagal_TransformHttpContentToHttps.WPClient.View;

namespace Seagal_TransformHttpContentToHttps.WPClient.Model
{
    public interface IPostMetaRepository
    {
        IEnumerable<Meta> GetPostMeta( IConnection connection);
        void UpdatePostMetas( IConnection connection, IEnumerable<Meta> postMetas );
   }
}
