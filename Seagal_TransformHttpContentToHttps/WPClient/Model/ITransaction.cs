using System;

namespace WPDatabaseWork.WPClient.Model
{
    public interface ITransaction : IDisposable
    {
        void Rollback();
        void Commit();
    }
}
