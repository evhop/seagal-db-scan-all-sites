using WPDatabaseWork.WPClient.Core;
using MySql.Data.MySqlClient;
using MySql.Data.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using WPDatabaseWork.WPClient.Model;
using WPDatabaseWork.WPClient.View;
using System.IO;

namespace WPDatabaseWork.WPClient
{
    public class WPClient : IWPClient
    {
        #region Fields

        private SSHTunnel _sshTunnel;
        private string _connectionString = null;
        private List<string> _dbTableSchema;
        #endregion

        #region Properties

        private List<string> PostsTables => _dbTableSchema.FindAll(t => t.Contains("posts"));
        private List<string> PostMetasTables => _dbTableSchema.FindAll(t => t.Contains("postmeta"));
        private List<string> CommentMetasTables => _dbTableSchema.FindAll(t => t.Contains("commentmeta"));
        private List<string> CommentsTables => _dbTableSchema.FindAll(t => t.Contains("comments"));
        private List<string> UsersTables => _dbTableSchema.FindAll(t => t.Contains("users"));
        private List<string> UserMetaTables => _dbTableSchema.FindAll(t => t.Contains("usermeta"));

        private string PostsTable => _dbTableSchema.First(t => t.Contains("posts"));
        private string PostMetasTable => _dbTableSchema.First(t => t.Contains("postmeta"));

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
        public void GetTableSchema(IConnection connection, string schema, string bloggId)
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
                    if (bloggId != "")
                    {
                        string tableName = reader.GetString("tableSchemaName");
                        if (tableName.Contains(bloggId))
                        {
                            _dbTableSchema.Add(tableName);
                        }
                    }
                    else
                    {
                        _dbTableSchema.Add(reader.GetString("tableSchemaName"));
                    }
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
            var command = new MySqlCommand("", connection.GetMySqlConnection())
            {
                CommandTimeout = 3600
            };

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

        public void CreateUpdateSiteFile(IConnection connection, string replaceFrom, string replaceTo)
        {
            var path = $@"C:\Users\evhop\Dokument\dumps\Http_Update_Domain.txt";

            foreach (var postTable in PostsTables)
            {
                var sql = new StringBuilder();

                using (var newStream = File.AppendText(path))
                {
                    newStream.WriteLine($"UPDATE {postTable} SET post_content = replace(post_content, '{replaceFrom}', '{replaceTo}') WHERE post_content like '%{replaceFrom}%';");
                    newStream.WriteLine($"UPDATE {postTable} SET post_excerpt = replace(post_excerpt, '{replaceFrom}', '{replaceTo}') WHERE post_excerpt like '%{replaceFrom}%';");
                    newStream.WriteLine($"UPDATE {postTable} SET post_content_filtered = replace(post_content_filtered, '{replaceFrom}', '{replaceTo}') WHERE post_content_filtered like '%{replaceFrom}%';");
                }
            }
        }

        public void UpdatePosts(IConnection connection, string replaceFrom, string replaceTo)
        {
            var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection());

            foreach (var postTable in PostsTables)
            {
                var sql = new StringBuilder();
                sql.Append($"UPDATE {postTable} SET post_content = replace(post_content, '{replaceFrom}', '{replaceTo}') WHERE post_content like '%{replaceFrom}%';");
                sql.Append($"UPDATE {postTable} SET post_excerpt = replace(post_excerpt, '{replaceFrom}', '{replaceTo}') WHERE post_excerpt like '%{replaceFrom}%';");
                sql.Append($"UPDATE {postTable} SET post_content_filtered = replace(post_content_filtered, '{replaceFrom}', '{replaceTo}') WHERE post_content_filtered like '%{replaceFrom}%';");

                command.CommandText = sql.ToString();
                command.ExecuteNonQuery();
            }
        }

        public void CreateSqlUpdatePostsfile(IConnection connection, IEnumerable<Post> posts, string colum, string path, string time)
        {
            var sqlNew = new StringBuilder();
            var sqlOld = new StringBuilder();

            var schemaIndex = posts.FirstOrDefault().SchemaTable.IndexOf('.');
            var schema = posts.FirstOrDefault().SchemaTable.Substring(0, schemaIndex - 1).Trim('\'').Trim('`');
            var pathNew = path + $"_{schema}_new_{time}.sql";
            var pathOld = path + $"_{schema}_old_{time}.sql";

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
            foreach (var postsTable in PostsTables)
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
        public IEnumerable<Post> GetPostsRegexp(IConnection connection, string colum, string regexp)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTables)
            {
                var sql = new StringBuilder();
                sql.Append($"SELECT ID, {colum}, post_date FROM {postsTable} where {colum} regexp '{regexp}' and post_status not in ('trash') and post_type not in ('revision');");

                var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection())
                {
                    CommandTimeout = 3600
                };
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        posts.Add(new Post
                        {
                            SchemaTable = postsTable,
                            Id = reader.GetUInt64("ID"),
                            Content = reader.GetString(colum),
                            Date = reader.GetDateTime("post_date").ToShortDateString(),
                            OldContent = reader.GetString(colum)
                        });
                    }
                }
            }
            return posts;
        }

        public IEnumerable<Post> GetPosts(IConnection connection, string colum, string likeSearch, int limit)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTables)
            {
                string limits = "";
                if (limit > 0)
                {
                    limits = $"LIMIT 0, {limit}";
                }
                var sql = new StringBuilder();
                sql.Append($"SELECT ID, {colum}, post_date FROM {postsTable} where {colum} like '{likeSearch}' and post_status not in ('trash') and post_type not in ('revision') {limits};");

                var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection())
                {
                    CommandTimeout = 3600
                };
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        posts.Add(new Post
                        {
                            SchemaTable = postsTable,
                            Id = reader.GetUInt64("ID"),
                            Content = reader.GetString(colum),
                            Date = reader.GetDateTime("post_date").ToShortDateString(),
                            OldContent = reader.GetString(colum)
                        });
                    }
                }
            }
            return posts;
        }

        public IEnumerable<Post> GetAttachments(IConnection connection)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTables)
            {
                var sql = new StringBuilder();
                sql.Append($"SELECT ID, guid, post_title FROM {postsTable} where post_type = 'attachment' and post_status not in ('trash');");

                var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection())
                {
                    CommandTimeout = 3600
                };
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        posts.Add(new Post
                        {
                            SchemaTable = postsTable,
                            Id = reader.GetUInt64("ID"),
                            Content = reader.GetString("guid"),
                            OldContent = reader.GetString("post_title")
                        });
                    }
                }
            }
            return posts;
        }

        public IEnumerable<Post> GetRecipeLinks(IConnection connection)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTables)
            {
                var sql = new StringBuilder();
                sql.Append($"select id, concat('http://www.alltommat.se/recept?recipeid=', b.meta_value) postContent, guid from {PostsTable} a inner join {PostMetasTable} b on a.id = b.post_id and a.post_type='recipe' and b.meta_key in ('_external_id') and post_status not in ('trash', 'private'); ");

                var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection())
                {
                    CommandTimeout = 3600
                };
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        posts.Add(new Post
                        {
                            Id = reader.GetUInt64("ID"),
                            Content = reader.GetString("postContent"),
                            OldContent = reader.GetString("guid")
                        });
                    }
                }
            }
            return posts;
        }

        #endregion

        #region IPostMetaRepository

        public void CreateSqlInsertPostMetasfile(IConnection connection, IEnumerable<Meta> metas, string path, string time)
        {
            var sqlNew = new StringBuilder();

            var schemaIndex = metas.FirstOrDefault().SchemaTable.IndexOf('.');
            var schema = metas.FirstOrDefault().SchemaTable.Substring(0, schemaIndex - 1).Trim('\'').Trim('`');
            var pathNew = path + $"_{schema}_{time}.sql";

            using (var newStream = File.AppendText(pathNew))
            {
                foreach (var meta in metas)
                {
                    var sqlStatement = $"INSERT INTO {meta.SchemaTable} (post_id, meta_key, meta_value) values ({meta.PostId}, '{meta.MetaKey}', '{meta.MetaValue}');";
                    newStream.WriteLine(sqlStatement);
                }
            }
        }

        public void InsertPostMetas(IConnection connection, IEnumerable<Meta> metas)
        {
            var remaining = metas;
            while (remaining.Any())
            {
                var toInsert = remaining;
                var skip = 0;
                var sql = new StringBuilder();
                foreach (var meta in toInsert)
                {
                    var sqlStatement = $"INSERT INTO {meta.SchemaTable} (post_id, meta_key, meta_value) values ({meta.PostId}, '{meta.MetaKey}', '{meta.MetaValue}');";

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

        public void UpdatePostMetas(IConnection connection, string replaceFrom, string replaceTo)
        {
            var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection());

            foreach (var postMetaTable in PostMetasTables)
            {
                var sql = new StringBuilder();
                sql.Append($"UPDATE {postMetaTable} SET meta_value = replace(meta_value, '{replaceFrom}', '{replaceTo}') WHERE meta_value like '%{replaceFrom}%';");

                command.CommandText = sql.ToString();
                command.ExecuteNonQuery();
            }
        }

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

        public IEnumerable<Meta> GetPostMeta(IConnection connection, string regexp)
        {
            var sql = new StringBuilder();

            var metas = new List<Meta>();
            foreach (var postMetaTable in PostMetasTables)
            {
                sql.AppendLine($"SELECT meta_id, meta_value FROM {postMetaTable} WHERE meta_value regexp '{regexp}';");
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

        public IEnumerable<Post> GetComments(IConnection connection, string regexp)
        {
            var comments = new List<Post>();
            foreach (var commentsTable in CommentsTables)
            {
                var sql = $"SELECT comment_ID, comment_content, comment_date FROM {commentsTable} where comment_content regexp '{regexp}'; ";
                var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection())
                {
                    CommandTimeout = 3600
                };
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comments.Add(new Post
                        {
                            SchemaTable = commentsTable,
                            Id = reader.GetUInt64("comment_ID"),
                            Content = reader.GetString("comment_content"),
                            OldContent = reader.GetString("comment_content"),
                            Date = reader.GetDateTime("comment_date").ToShortDateString()
                        });
                    }
                }
            }

            return comments;
        }

        public void UpdateComments(IConnection connection, IEnumerable<Post> comments)
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

        public void UpdateComments(IConnection connection, string replaceFrom, string replaceTo)
        {
            var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection());

            foreach (var commentTable in CommentsTables)
            {
                var sql = new StringBuilder();
                sql.Append($"UPDATE {commentTable} SET comment_content = replace(comment_content, '{replaceFrom}', '{replaceTo}') WHERE comment_content like '%{replaceFrom}%';");

                command.CommandText = sql.ToString();
                command.ExecuteNonQuery();
            }
        }

        #endregion

        #region ICommentMetaRepository

        public void UpdateCommentMetas(IConnection connection, string replaceFrom, string replaceTo)
        {
            var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection());

            foreach (var commentMetaTable in CommentMetasTables)
            {
                var sql = new StringBuilder();
                sql.Append($"UPDATE {commentMetaTable} SET meta_value = replace(meta_value, '{replaceFrom}', '{replaceTo}') WHERE meta_value like '%{replaceFrom}%';");

                command.CommandText = sql.ToString();
                command.ExecuteNonQuery();
            }
        }

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

        public IEnumerable<Meta> GetCommentMeta(IConnection connection, string regexp)
        {
            var metas = new List<Meta>();

            foreach (var commentMetaTable in CommentMetasTables)
            {
                var sql = new StringBuilder();
                sql.AppendLine($"SELECT meta_id, meta_value FROM {commentMetaTable} WHERE meta_value regexp '{regexp}';");

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
                            //return (int)(reader.GetInt32( "Value" ) * 0.2);
                            return 100000;
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