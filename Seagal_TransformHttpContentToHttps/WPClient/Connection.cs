using System;
using WPDatabaseWork.WPClient.Model;
using MySql.Data.MySqlClient;

namespace WPDatabaseWork.WPClient
{
    public class Connection : IConnection
    {
        public MySqlConnection MySqlConnection { get; private set; }

        public Connection( MySqlConnection connection ) => MySqlConnection = connection ?? throw new ArgumentNullException( nameof( connection ) );

        public ITransaction BeginTransaction() => new Transaction( MySqlConnection.BeginTransaction() );
        public void Dispose()
        {
            if( MySqlConnection != null )
            {
                MySqlConnection.Dispose();
                MySqlConnection = null;
            }
        }
    }
}
