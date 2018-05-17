using System;

namespace Seagal_TransformHttpContentToHttps.WPClient.Model
{
    public interface ITransaction : IDisposable
    {
        void Rollback();
        void Commit();
    }
}
