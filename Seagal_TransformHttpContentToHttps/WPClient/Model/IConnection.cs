using System;

namespace WPDatabaseWork.WPClient.Model
{
    public interface IConnection : IDisposable
    {
        ITransaction BeginTransaction();
    }
}
