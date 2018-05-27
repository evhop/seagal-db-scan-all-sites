using System;

namespace Fallback_blogg.WPClient.Model
{
    public interface IConnection : IDisposable
    {
        ITransaction BeginTransaction();
    }
}
