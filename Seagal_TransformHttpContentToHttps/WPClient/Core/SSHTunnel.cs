using Renci.SshNet;
using System;

namespace WPDatabaseWork.WPClient.Core
{
    public class SSHTunnel
    {
        private SshClient _sshClient = null;
        private uint _localPort = 3306;
        private uint _remotePort = 3306;
        private string _host;
        private string _username;
        private string _password;

        public SSHTunnel( uint localPort, uint remotePort, string host, string username, string password )
        {
            _localPort = localPort;
            _remotePort = remotePort;
            _username = username;
            _password = password;
            _host = host;
        }

        public void Start()
        {
            var connectionInfo = new PasswordConnectionInfo( _host, _username, _password );
            _sshClient = new SshClient( connectionInfo );
            _sshClient.Connect();

            if( !_sshClient.IsConnected )
            {
                throw new Exception( "Connection failed" );
            }

            var fwd = new ForwardedPortLocal( "127.0.0.1", _localPort, _host, _remotePort );
            _sshClient.AddForwardedPort( fwd );
            fwd.Start();

            if( !fwd.IsStarted )
            {
                throw new Exception( "Forwarding failed" );
            }
        }

        public void Stop()
        {
            if( _sshClient != null )
            {
                _sshClient.Disconnect();
                _sshClient.Dispose();

                _sshClient = null;
            }
        }
    }
}
