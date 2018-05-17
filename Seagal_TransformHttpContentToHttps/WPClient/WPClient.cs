using Seagal_TransformHttpContentToHttps.WPClient.Core;
using MySql.Data.MySqlClient;
using MySql.Data.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Seagal_TransformHttpContentToHttps.WPClient.Model;
using Seagal_TransformHttpContentToHttps.WPClient.View;

namespace Seagal_TransformHttpContentToHttps.WPClient
{
    public class WPClient : IWPClient
    {
        #region Fields

        private SSHTunnel _sshTunnel;
        private ITableNameGenerator _tableNameGenerator;
        private string _connectionString = null;
        private List<TableSchema> _dbTableSchema;

        #endregion

        #region Properties

        private string PostsTable => _tableNameGenerator.GetName("posts");
        private string PostMetaTable => _tableNameGenerator.GetName("postmeta");
        private string CommentMetaTable => _tableNameGenerator.GetName("commentmeta");
        private string CommentsTable => _tableNameGenerator.GetName("comments");
        private string UsersTable => _tableNameGenerator.GetName("users");
        private string UserMetaTable => _tableNameGenerator.GetName("usermeta");

        public int MaxAllowedPacket { get; }

        #endregion

        #region Constructors

        public WPClient(string connectionString, ITableNameGenerator tableNameGenerator)
        {
            _tableNameGenerator = tableNameGenerator ?? throw new ArgumentNullException(nameof(tableNameGenerator));
            _connectionString = connectionString;
            MaxAllowedPacket = GetMaxAllowedPacket();
        }

        public WPClient(string connectionString, ITableNameGenerator tableNameGenerator, string host, string username, string password)
            : this(connectionString, tableNameGenerator) => StartSshTunnel(host, username, password);

        #endregion

        #region Methods
        public void GetTableSchema(IConnection connection)
        {
            var sql = $"SELECT distinct TABLE_SCHEMA, table_name FROM information_schema.tables " +
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

            _dbTableSchema = new List<TableSchema>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    TableSchema tableSchema = new TableSchema()
                    {
                        Schema = reader.GetString("TABLE_SCHEMA"),
                        Name = reader.GetString("table_name")
                    };
                    tableSchema.SchemaAndName = tableSchema.Schema + "." + tableSchema.Name;

                    _dbTableSchema.Add(tableSchema);
                }
            }
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
                    var escapedGuid = MySqlHelper.EscapeString(content.Guid);
                    var escapedExcerpt = MySqlHelper.EscapeString(content.Excerpt);
                    var escapedContentFiltered = MySqlHelper.EscapeString(content.ContentFiltered);
                    var escapedContent = MySqlHelper.EscapeString(content.Content);
                    var sqlStatement = $"UPDATE {PostsTable} SET guid = '{escapedGuid}', content = '{escapedContent}', ContentFiltered = '{escapedContentFiltered}', Excerpt = '{escapedExcerpt}' WHERE ID = {content.Id};";

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

        public IEnumerable<Post> GetPosts()
        {
            using (var connection = CreateConnection())
            {
                return GetPosts(connection);
            }
        }

        public IEnumerable<Post> GetPosts(IConnection connection)
        {
            var sql = new StringBuilder();
            sql.Append($"SELECT ID, post_content, guid, post_excerpt, post_content_filtered FROM {PostsTable};");

            var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection());
            using (var reader = command.ExecuteReader())
            {
                var posts = new List<Post>();
                while (reader.Read())
                {
                    posts.Add(new Post
                    {
                        Id = reader.GetUInt64("ID"),
                        Content = reader.GetString("post_content"),
                        Guid = reader.GetString("guid"),
                        Excerpt = reader.GetString("post_excerpt"),
                        ContentFiltered = reader.GetString("post_content_filtered")
                    });
                }

                return posts;
            }
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
                    var sqlStatement = $"UPDATE {PostMetaTable} SET meta_value = '{MySqlHelper.EscapeString(meta.MetaValue)}' WHERE meta_id = {meta.MetaId};";

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

        public IEnumerable<Meta> GetPostMeta(IConnection connection)
        {
            var sql = new StringBuilder();
            sql.AppendLine($"SELECT meta_id, meta_value FROM {PostMetaTable} WHERE meta_value like '%http://%'");

            var command = new MySqlCommand(@sql.ToString(), connection.GetMySqlConnection())
            {
                CommandTimeout = 3600
            };

            using (var reader = command.ExecuteReader())
            {
                var metas = new List<Meta>();
                while (reader.Read())
                {
                    var meta = new Meta
                    {
                        MetaId = reader.GetUInt64("meta_id"),
                        MetaValue = reader.GetString("meta_value")
                    };

                    metas.Add(meta);
                }

                return metas;
            }
        }

        #endregion

        #region ICommentRepository

        public IEnumerable<Comment> GetComments()
        {
            using (var connection = CreateConnection())
            {
                return GetComments(connection);
            }
        }

        public IEnumerable<Comment> GetComments(IConnection connection)
        {
            var sql = $"SELECT comment_ID, comment_author_url, comment_content FROM {CommentsTable}";

            var command = new MySqlCommand(sql, connection.GetMySqlConnection());
            using (var reader = command.ExecuteReader())
            {
                var comments = new List<Comment>();
                while (reader.Read())
                {
                    comments.Add(new Comment
                    {
                        Id = reader.GetUInt64("comment_ID"),
                        AuthorUrl = reader.GetString("comment_author_url"),
                        Content = reader.GetString("comment_content")
                    });
                }

                return comments;
            }
        }
        public void UpdateComments(IEnumerable<Comment> comments)
        {
            if (!comments.Any())
            {
                return;
            }

            using (var connection = CreateConnection())
            {
                var transaction = connection.BeginTransaction();

                try
                {
                    UpdateComments(connection, comments);
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                }
            }
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
                    var escapedAuthorUrl = MySqlHelper.EscapeString(content.AuthorUrl);
                    var escapedContent = MySqlHelper.EscapeString(content.Content);
                    var sqlStatement = $"UPDATE {CommentsTable} SET comment_author_url = '{escapedAuthorUrl}', comment_content = '{escapedContent}' WHERE comment_ID = {content.Id};";

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
                    var sqlStatement = $"UPDATE {CommentMetaTable} SET meta_value = '{MySqlHelper.EscapeString(meta.MetaValue)}' WHERE meta_id = {meta.MetaId};";

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

        public IEnumerable<Meta> GetCommentMeta(IConnection connection)
        {
            var sql = new StringBuilder();
            sql.AppendLine($"SELECT meta_id, meta_value FROM {CommentMetaTable} WHERE meta_value like '%http://%'");

            var command = new MySqlCommand(@sql.ToString(), connection.GetMySqlConnection())
            {
                CommandTimeout = 3600
            };

            using (var reader = command.ExecuteReader())
            {
                var metas = new List<Meta>();
                while (reader.Read())
                {
                    var meta = new Meta
                    {
                        MetaId = reader.GetUInt64("meta_id"),
                        MetaValue = reader.GetString("meta_value")
                    };

                    metas.Add(meta);
                }

                return metas;
            }
        }

        #endregion


        #region IUserRepository

        public IEnumerable<User> GetUsers()
        {
            using( var connection = CreateConnection() )
            {
                return GetUsers( connection );
            }
        }

        public IEnumerable<User> GetUsers( IConnection connection )
        {
            var sql = $"SELECT u.ID, u.user_url FROM {UsersTable} AS u;";

            var command = new MySqlCommand( sql, connection.GetMySqlConnection() )
            {
                CommandTimeout = 3600
            };

            using( var reader = command.ExecuteReader() )
            {
                var users = new List<User>();
                while( reader.Read() )
                {
                    users.Add(new User()
                    {
                        Id = reader.GetUInt64( "ID" ),
                        Url = reader.GetString("user_url")
                    });
                }

                return users;
            }
        }

        public void UpdateUsers(IEnumerable<User> users)
        {
            if (!users.Any())
            {
                return;
            }

            using (var connection = CreateConnection())
            {
                var transaction = connection.BeginTransaction();

                try
                {
                    UpdateUsers(connection, users);
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                }
            }
        }

        public void UpdateUsers(IConnection connection, IEnumerable<User> users)
        {
            var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection());

            var remaining = users;
            while (remaining.Any())
            {
                var sql = new StringBuilder();

                var toUpdate = remaining;
                var skip = 0;
                foreach (var content in toUpdate)
                {
                    var escapedUrl = MySqlHelper.EscapeString(content.Url);
                    var sqlStatement = $"UPDATE {UsersTable} SET user_url = '{escapedUrl}' WHERE ID = {content.Id};";

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

        #region IUserMetaRepository

        public void UpdateUserMetas(IConnection connection, IEnumerable<Meta> userMetas)
        {
            var remaining = userMetas;
            while (remaining.Any())
            {
                var toInsert = remaining;
                var skip = 0;
                var sql = new StringBuilder();
                foreach (var meta in toInsert)
                {
                    var sqlStatement = $"UPDATE {UserMetaTable} SET meta_value = '{MySqlHelper.EscapeString(meta.MetaValue)}' WHERE umeta_id = {meta.MetaId};";

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

        public IEnumerable<Meta> GetUserMeta(IConnection connection)
        {
            var sql = new StringBuilder();
            sql.AppendLine($"SELECT umeta_id, meta_value FROM {UserMetaTable} WHERE meta_value like '%http://%'");

            var command = new MySqlCommand(@sql.ToString(), connection.GetMySqlConnection())
            {
                CommandTimeout = 3600
            };

            using (var reader = command.ExecuteReader())
            {
                var metas = new List<Meta>();
                while (reader.Read())
                {
                    var meta = new Meta
                    {
                        MetaId = reader.GetUInt64("umeta_id"),
                        MetaValue = reader.GetString("meta_value")
                    };

                    metas.Add(meta);
                }

                return metas;
            }
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