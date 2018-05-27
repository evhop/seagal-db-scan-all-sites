using System;

namespace Fallback_blogg.WPClient.Model
{
    public interface ITransaction : IDisposable
    {
        void Rollback();
        void Commit();
    }
}
