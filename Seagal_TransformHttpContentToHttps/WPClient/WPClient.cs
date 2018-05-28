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
        private List<string> PostMetaTable => _dbTableSchema.FindAll(t => t.Contains("postmeta"));
        private List<string> CommentMetaTable => _dbTableSchema.FindAll(t => t.Contains("commentmeta"));
        private List<string> CommentsTable => _dbTableSchema.FindAll(t => t.Contains("comments"));
        private List<string> UsersTable => _dbTableSchema.FindAll(t => t.Contains("users"));
        private List<string> UserMetaTable => _dbTableSchema.FindAll(t => t.Contains("usermeta"));

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
                 "and (table_name like 'wp%posts' or " +
                    "table_name like 'wp%postmeta' or " +
                    "table_name like 'wp%comments' or " +
                    "table_name like 'wp%commentmeta' or " +
                    "table_name like 'wp%users' or " +
                    "table_name like 'wp%usermeta') " +
                    "; ";

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
                    "where(table_name like 'wp%posts' or " +
                    "table_name like 'wp%postmeta' or " +
                    "table_name like 'wp%comments' or " +
                    "table_name like 'wp%commentmeta' or " +
                    "table_name like 'wp%users' or " +
                    "table_name like 'wp%usermeta') " +
                    "; ";

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

        public void UpdatePosts(IConnection connection, IEnumerable<Post> posts, string colum)
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
                    var sqlStatement = $"UPDATE {content.SchemaTable} SET {colum} = '{escapedContent}' WHERE ID = {content.Id};";

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

        public void CreateSqlUpdatePostsfile(IConnection connection, IEnumerable<Post> posts, string colum, string path, string time)
        {
            var sqlNew = new StringBuilder();
            var sqlOld = new StringBuilder();

            var schemaIndex = posts.FirstOrDefault().SchemaTable.IndexOf('.');
            var schema = posts.FirstOrDefault().SchemaTable.Substring(0, schemaIndex - 1).Trim('\'').Trim('`');
            var pathNew = path + $"_{schema}_new_{time}.txt";
            var pathOld = path + $"_{schema}_old_{time}.txt";

            using (var newStream = File.AppendText(pathNew))
            {
                using (var oldStream = File.AppendText(pathOld))
                {

                    foreach (var content in posts)
                    {
                        var escapedContent = MySqlHelper.EscapeString(content.Content);
                        var sqlStatement = $"UPDATE {content.SchemaTable} SET {colum} = '{escapedContent}' WHERE ID = {content.Id};";
                        newStream.WriteLine(sqlStatement);
                        escapedContent = MySqlHelper.EscapeString(content.OldContent);
                        sqlStatement = $"UPDATE {content.SchemaTable} SET {colum} = '{escapedContent}' WHERE ID = {content.Id};";
                        oldStream.WriteLine(sqlStatement);
                    }
                }
            }
        }

        public IEnumerable<Post> GetPosts(IConnection connection, string colum)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTable)
            {
                var sql = new StringBuilder();
                sql.Append($"SELECT ID, {colum} FROM {postsTable} where {colum} REGEXP ' (href|src)=http';");

                var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection());
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        posts.Add(new Post
                        {
                            SchemaTable = postsTable,
                            Id = reader.GetUInt64("ID"),
                            Content = reader.GetString(colum),
                            OldContent = reader.GetString(colum)
                        });
                    }
                }
            }
            return posts;
        }
        public IEnumerable<Post> GetPosts(IConnection connection, string colum, string likeSearch)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTable)
            {
                var sql = new StringBuilder();
                sql.Append($"SELECT ID, {colum} FROM {postsTable} where {colum} like '%{likeSearch}%';");

                var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection());
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        posts.Add(new Post
                        {
                            SchemaTable = postsTable,
                            Id = reader.GetUInt64("ID"),
                            Content = reader.GetString(colum),
                            OldContent = reader.GetString(colum)
                        });
                    }
                }
            }
            return posts;
        }

        #endregion

        #region IPostMetaRepository

        public void UpdatePostMetas(IConnection connection, IEnumerable<Meta> postMetas)
        {
            var remaining = postMetas;
            while (remaining.Any())
            {
                var toInsert = remaining;
                var skip = 0;
                var sql = new StringBuilder();
                foreach (var meta in toInsert)
                {
                    var sqlStatement = $"UPDATE {meta.SchemaTable} SET meta_value = '{MySqlHelper.EscapeString(meta.MetaValue)}' WHERE meta_id = {meta.MetaId};";

                    if ((sqlStatement.Length + sql.Length) >= MaxAllowedPacket)
                    {
                        break;
                    }

                    skip++;
                    sql.AppendLine(sqlStatement);
                }

                using (var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection()))
                {
                    command.ExecuteNonQuery();
                }

                remaining = remaining.Skip(skip);
            }
        }

        public IEnumerable<Meta> GetPostMeta(IConnection connection, string likeSearch)
        {
            var sql = new StringBuilder();

            var metas = new List<Meta>();
            foreach (var postMetaTable in PostMetaTable)
            {
                sql.AppendLine($"SELECT meta_id, meta_value FROM {postMetaTable} WHERE meta_value like '%{likeSearch}%';");
                var command = new MySqlCommand(@sql.ToString(), connection.GetMySqlConnection())
                {
                    CommandTimeout = 3600
                };

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var meta = new Meta
                        {
                            SchemaTable = postMetaTable,
                            MetaId = reader.GetUInt64("meta_id"),
                            MetaValue = reader.GetString("meta_value")
                        };

                        metas.Add(meta);
                    }
                }
            }
            return metas;
        }

        #endregion

        #region ICommentRepository

        public IEnumerable<Comment> GetComments(IConnection connection, string likeSearch)
        {
            var comments = new List<Comment>();
            foreach (var commentsTable in CommentsTable)
            {
                var sql = $"SELECT comment_ID, comment_content FROM {commentsTable} where comment_content like '%{likeSearch}%'; ";
                var command = new MySqlCommand(sql, connection.GetMySqlConnection());
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comments.Add(new Comment
                        {
                            SchemaTable = commentsTable,
                            Id = reader.GetUInt64("comment_ID"),
                            Content = reader.GetString("comment_content")
                        });
                    }
                }
            }

            return comments;
        }

        public void UpdateComments(IConnection connection, IEnumerable<Comment> comments)
        {
            var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection());

            var remaining = comments;
            while (remaining.Any())
            {
                var sql = new StringBuilder();

                var toUpdate = remaining;
                var skip = 0;
                foreach (var content in toUpdate)
                {
                    var escapedContent = MySqlHelper.EscapeString(content.Content);
                    var sqlStatement = $"UPDATE {content.SchemaTable} SET comment_content = '{escapedContent}' WHERE comment_ID = {content.Id};";

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

        #endregion

        #region ICommentMetaRepository

        public void UpdateCommentMetas(IConnection connection, IEnumerable<Meta> commentMetas)
        {
            var remaining = commentMetas;
            while (remaining.Any())
            {
                var toInsert = remaining;
                var skip = 0;
                var sql = new StringBuilder();
                foreach (var meta in toInsert)
                {
                    var sqlStatement = $"UPDATE {meta.SchemaTable} SET meta_value = '{MySqlHelper.EscapeString(meta.MetaValue)}' WHERE meta_id = {meta.MetaId};";

                    if ((sqlStatement.Length + sql.Length) >= MaxAllowedPacket)
                    {
                        break;
                    }

                    skip++;
                    sql.AppendLine(sqlStatement);
                }

                using (var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection()))
                {
                    command.ExecuteNonQuery();
                }

                remaining = remaining.Skip(skip);
            }
        }

        public IEnumerable<Meta> GetCommentMeta(IConnection connection, string likeSearch)
        {
            var metas = new List<Meta>();

            foreach (var commentMetaTable in CommentMetaTable)
            {
                var sql = new StringBuilder();
                sql.AppendLine($"SELECT meta_id, meta_value FROM {commentMetaTable} WHERE meta_value like '%{likeSearch}%';");

                var command = new MySqlCommand(@sql.ToString(), connection.GetMySqlConnection())
                {
                    CommandTimeout = 3600
                };

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var meta = new Meta
                        {
                            SchemaTable = commentMetaTable,
                            MetaId = reader.GetUInt64("meta_id"),
                            MetaValue = reader.GetString("meta_value")
                        };

                        metas.Add(meta);
                    }
                }
            }
            return metas;
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