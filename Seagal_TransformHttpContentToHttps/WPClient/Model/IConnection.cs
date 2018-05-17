using System;

namespace Seagal_TransformHttpContentToHttps.WPClient.Model
{
    public interface IConnection : IDisposable
    {
        ITransaction BeginTransaction();
    }
}
