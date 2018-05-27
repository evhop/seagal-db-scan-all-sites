using Fallback_blogg.WPClient.Core;
using MySql.Data.MySqlClient;
using MySql.Data.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Fallback_blogg.WPClient.Model;
using Fallback_blogg.WPClient.View;
using System.IO;

namespace Fallback_blogg.WPClient
{
    public class WPClient : IWPClient
    {
        #region Fields

        private SSHTunnel _sshTunnel;
        private string _connectionString = null;
        private List<string> _dbTableSchema;
        #endregion

        #region Properties

        private List<string> PostsTable => _dbTableSchema.FindAll(t => t.Contains("posts"));

        public int MaxAllowedPacket { get; }

        #endregion

        #region Constructors

        public WPClient(string connectionString)
        {
            _connectionString = connectionString;
            MaxAllowedPacket = GetMaxAllowedPacket();
        }

        public WPClient(string connectionString, string host, string username, string password)
            : this(connectionString) => StartSshTunnel(host, username, password);

        #endregion

        #region Methods
        public void GetTableSchema(IConnection connection, string schema)
        {
            string databaseSchema = "`" + schema + "`.";
            var sql = $"SELECT distinct concat('{databaseSchema}',table_name) as tableSchemaName FROM information_schema.tables " +
                $"WHERE TABLE_SCHEMA = '{schema}'" +
                 "and table_name like 'wp%posts' ; ";

            var command = new MySqlCommand(sql, connection.GetMySqlConnection())
            {
                CommandTimeout = 3600
            };

            _dbTableSchema = new List<string>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    _dbTableSchema.Add(reader.GetString("tableSchemaName"));
                }
            }
        }

        public IEnumerable<string> GetSchema(IConnection connection)
        {
            var sql = $"SELECT distinct TABLE_SCHEMA FROM information_schema.tables " +
                    "where table_name like 'wp%posts'; ";

            var command = new MySqlCommand(sql, connection.GetMySqlConnection())
            {
                CommandTimeout = 3600
            };

            List<string> schemas = new List<string>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    schemas.Add(reader.GetString("TABLE_SCHEMA"));
                }
            }
            return schemas;
        }

        public void StartSshTunnel(string host, string username, string password)
        {
            _sshTunnel = new SSHTunnel(3306, 3306, host, username, password);
            _sshTunnel.Start();
        }

        public void StopSshTunnel()
        {
            if (_sshTunnel != null)
            {
                _sshTunnel.Stop();
                _sshTunnel = null;
            }
        }

        public IConnection CreateConnection() => CreateConnection(_connectionString);

        public static IConnection CreateConnection(string connectionString)
        {
            var connection = new MySqlConnection(connectionString);

            connection.Open();
            if (connection.State != ConnectionState.Open)
            {
                return null;
            }

            return new Connection(connection);
        }

        public void Dispose() => StopSshTunnel();

        #endregion

        #region IPostRepository

        public void UpdatePosts(IEnumerable<Post> posts)
        {
            if (!posts.Any())
            {
                return;
            }

            using (var connection = CreateConnection())
            {
                var transaction = connection.BeginTransaction();

                try
                {
                    UpdatePosts(connection, posts);
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                }
            }
        }

        public void UpdatePosts(IConnection connection, IEnumerable<Post> posts)
        {
            var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection());

            var remaining = posts;
            while (remaining.Any())
            {
                var sql = new StringBuilder();

                var toUpdate = remaining;
                var skip = 0;
                foreach (var content in toUpdate)
                {
                    var escapedContent = MySqlHelper.EscapeString(content.Content);
                    var sqlStatement = $"UPDATE {content.SchemaTable} SET post_content = '{escapedContent}' WHERE ID = {content.Id};";

                    if ((sqlStatement.Length + sql.Length) >= MaxAllowedPacket)
                    {
                        break;
                    }

                    skip++;
                    sql.Append(sqlStatement);
                }

                command.CommandText = sql.ToString();
                command.ExecuteNonQuery();

                remaining = remaining.Skip(skip);
            }
        }

        public void CreateSqlUpdatePostsfile(IConnection connection, IEnumerable<Post> posts)
        {
            var sqlNew = new StringBuilder();
            var sqlOld = new StringBuilder();

            var time = DateTime.Now.ToString("yyyyMMddHHmmss");
            var schemaIndex = posts.FirstOrDefault().SchemaTable.IndexOf('.');
            var schema = posts.FirstOrDefault().SchemaTable.Substring(0, schemaIndex - 1).Trim('\'').Trim('`');
            var pathNew = $@"C:\Users\evhop\source\repos\fallback_Blogg\{schema}_new_{time}.txt";
            var pathOld = $@"C:\Users\evhop\source\repos\fallback_Blogg\{schema}_old_{time}.txt";

            if (File.Exists(pathNew))
            {
                File.Delete(pathNew);
            }

            if (File.Exists(pathOld))
            {
                File.Delete(pathOld);
            }

            using (var newStream = File.AppendText(pathNew))
            {
                using (var oldStream = File.AppendText(pathOld))
                {

                    foreach (var content in posts)
                    {
                        var escapedContent = MySqlHelper.EscapeString(content.Content);
                        var sqlStatement = $"UPDATE {content.SchemaTable} SET post_content = '{escapedContent}' WHERE ID = {content.Id};";
                        newStream.WriteLine(sqlStatement);
                        escapedContent = MySqlHelper.EscapeString(content.OldContent);
                        sqlStatement = $"UPDATE {content.SchemaTable} SET post_content = '{escapedContent}' WHERE ID = {content.Id};";
                        oldStream.WriteLine(sqlStatement);
                    }
                }
            }
        }

        public IEnumerable<Post> GetPosts(IConnection connection)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTable)
            {
                var sql = new StringBuilder();
                sql.Append($"SELECT ID, post_content FROM {postsTable} where post_content REGEXP ' (href|src)=http';");

                var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection());
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        posts.Add(new Post
                        {
                            SchemaTable = postsTable,
                            Id = reader.GetUInt64("ID"),
                            Content = reader.GetString("post_content"),
                            OldContent = reader.GetString("post_content")
                        });
                    }
                }
            }
            return posts;
        }

        #endregion

        #region Helpers

        private int GetMaxAllowedPacket()
        {
            using( var connection = CreateConnection() )
            {
                var mysqlConnection = connection.GetMySqlConnection();
                using( var mysqlCommand = mysqlConnection.CreateCommand() )
                {
                    mysqlCommand.CommandText = "SHOW VARIABLES LIKE 'max_allowed_packet';";
                    using( var reader = mysqlCommand.ExecuteReader() )
                    {
                        if( reader.Read() )
                        {
                            return (int)(reader.GetInt32( "Value" ) * 0.9);
                        }
                        else
                        {
                            throw new Exception( "Failed to read max_allowed_packet" );
                        }
                    }
                }
            }
        }
        #endregion
    }
}