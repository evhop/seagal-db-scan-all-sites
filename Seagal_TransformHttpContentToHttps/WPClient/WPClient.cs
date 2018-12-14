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
        private string OptionTable => _dbTableSchema.First(t => t.Contains("options"));

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
                    "table_name like 'wp%options' or " +
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
                    "table_name like 'wp%options' or " +
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

        public void InsertPosts(IConnection connection, IEnumerable<Attachment> posts)
        {
            var remaining = posts;
            while (remaining.Any())
            {
                using (var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection())
                {
                    CommandTimeout = 600
                })
                {
                    var sql = new StringBuilder($@"INSERT INTO {PostsTable} (post_author, post_date, post_date_gmt, post_content, post_title, post_excerpt, post_status, comment_status, ping_status, post_password, post_name, to_ping, pinged, post_modified, post_modified_gmt, post_content_filtered, post_parent, guid, menu_order, post_type, post_mime_type, comment_count) VALUES ");

                    var toUpdate = remaining;
                    ulong skip = 0;
                    string defaultTime = "0001-01-01 00:00:00";
                    foreach (var post in toUpdate)
                    {
                        var sqlStatement = $@"({post.Author},
                        '{MySqlHelper.EscapeString(post.Date.ToString("yyyy-MM-dd HH:mm:ss"))}',
                        '{MySqlHelper.EscapeString(post.DateGMT.ToString("yyyy-MM-dd HH:mm:ss"))}',
                        '{MySqlHelper.EscapeString(post.Content ?? string.Empty)}',
                        '{MySqlHelper.EscapeString(post.Title ?? string.Empty)}',
                        '{MySqlHelper.EscapeString(string.Empty)}',
                        '{MySqlHelper.EscapeString(post.Status ?? string.Empty)}',
                        '{MySqlHelper.EscapeString(post.CommentStatus ?? string.Empty)}',
                        '{MySqlHelper.EscapeString(post.PingStatus ?? string.Empty)}',
                        '{MySqlHelper.EscapeString(string.Empty)}',
                        '{MySqlHelper.EscapeString(post.Name ?? string.Empty)}',
                        '{MySqlHelper.EscapeString(string.Empty)}',
                        '{MySqlHelper.EscapeString(string.Empty)}',
                        '{MySqlHelper.EscapeString(defaultTime)}',
                        '{MySqlHelper.EscapeString(defaultTime)}',
                        '{MySqlHelper.EscapeString(string.Empty)}',
                        {post.ParentId},
                        '{MySqlHelper.EscapeString(post.Guid ?? string.Empty)}',
                        {0},
                        '{MySqlHelper.EscapeString(post.Type ?? string.Empty)}',
                        '{MySqlHelper.EscapeString(post.MimeType ?? string.Empty)}',
                        {0})";

                        if ((sqlStatement.Length + sql.Length) >= MaxAllowedPacket)
                        {
                            break;
                        }

                        if (skip > 0)
                        {
                            sql.Append(", ");
                        }

                        sql.Append(sqlStatement);
                        skip++;
                    }

                    sql.Append(";");
                    sql.Append("SELECT LAST_INSERT_ID();");

                    command.CommandText = sql.ToString();
                    var id = (ulong)command.ExecuteScalar();

                    ulong index = id;
                    foreach (var page in toUpdate.Take((int)skip))
                    {
                        page.Id = index++;
                        page.Metas.ForEach(x => x.PostId = page.Id);
                        InsertPostMetas(connection, page.Metas);
                    }

                    remaining = remaining.Skip((int)skip);
                }
            }
        }

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

        public void UpdateParentIds(IConnection connection, IEnumerable<Post> posts)
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
                    var sqlStatement = $"UPDATE {content.SchemaTable} SET post_parent = {content.ParentId} WHERE ID = {content.Id};";

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
            command.CommandTimeout = 3600;
            foreach (var postTable in PostsTables)
            {
                var count = CountPosts(connection, postTable, replaceFrom);
                if (count > 0)
                {
                    Console.WriteLine($"Updates {count} for {postTable} urlsrc='{replaceFrom}'");
                    var sql = new StringBuilder();
                    sql.Append($"UPDATE {postTable} SET post_content = replace(post_content, '{replaceFrom}', '{replaceTo}') WHERE post_content like '%{replaceFrom}%';");
                    sql.Append($"UPDATE {postTable} SET post_excerpt = replace(post_excerpt, '{replaceFrom}', '{replaceTo}') WHERE post_excerpt like '%{replaceFrom}%';");
                    sql.Append($"UPDATE {postTable} SET post_content_filtered = replace(post_content_filtered, '{replaceFrom}', '{replaceTo}') WHERE post_content_filtered like '%{replaceFrom}%';");

                    command.CommandText = sql.ToString();
                    command.ExecuteNonQuery();
                }
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
        public void CreateSqlReplaceUpdatePostsfile(IConnection connection, IEnumerable<Post> posts, string colum, string path, string time)
        {
            var sqlNew = new StringBuilder();

            var schemaIndex = posts.FirstOrDefault().SchemaTable.IndexOf('.');
            var schema = posts.FirstOrDefault().SchemaTable.Substring(0, schemaIndex - 1).Trim('\'').Trim('`');
            var pathNew = path + $"_{schema}_{time}.sql";

            using (var newStream = File.AppendText(pathNew))
            {
                foreach (var post in posts)
                {
                    var sqlStatement = $"UPDATE {post.SchemaTable} SET {colum} = replace({colum}, '{post.OldContent}','{post.Content}') WHERE ID = {post.Id};";
                    newStream.WriteLine(sqlStatement);
                }
            }
        }

        public IEnumerable<Post> GetPosts(IConnection connection, string colum)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTables)
            {
                var sql = new StringBuilder();
                sql.Append($"SELECT ID, {colum}, guid FROM {postsTable} where {colum} REGEXP ' (href|src)=http';");

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
                            OldContent = reader.GetString(colum),
                            Guid = reader.GetString("guid")
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
                sql.Append($"SELECT ID, {colum}, post_date, guid FROM {postsTable} where {colum} regexp '{regexp}' and post_status not in ('trash') and post_type not in ('revision');");

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
                            OldContent = reader.GetString(colum),
                            Guid = reader.GetString("guid")
                        });
                    }
                }
            }
            return posts;
        }

        public IEnumerable<Post> GetPostsWithImagesAndNoAttachments(IConnection connection, string likeSearch)
        {
            var posts = new List<Post>();
            foreach (var postsTable in PostsTables)
            {
                var sql = new StringBuilder();
                sql.Append($"SELECT post.ID, post.post_content, post.post_date, post.guid FROM {postsTable} post " +
                            $" left join {postsTable} attachment on post.id = attachment.post_parent and attachment.post_type = 'attachment'" +
                            $" where post.post_content like '{likeSearch}'" +
                            $" and post.post_type='post' and post.post_status not in ('trash') and post.post_type='post'" +
                            $" and attachment.id is null;");



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
                            Content = reader.GetString("post_content"),
                            Date = reader.GetDateTime("post_date").ToShortDateString(),
                            OldContent = reader.GetString("post_content"),
                            Guid = reader.GetString("guid")
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
                sql.Append($"SELECT ID, {colum}, post_date, guid FROM {postsTable} where {colum} like '{likeSearch}' and post_status not in ('trash') and post_type not in ('revision') {limits};");

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
                            OldContent = reader.GetString(colum),
                            Guid = reader.GetString("guid")
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

        public long CountPosts(IConnection connection, string postTable, string searchValue)
        {
            var sql = new StringBuilder();
            sql.Append($"SELECT COUNT(*) FROM {postTable} where post_content like '%{searchValue}%';");

            var command = new MySqlCommand(sql.ToString(), connection.GetMySqlConnection());
            command.CommandTimeout = 3600;
            return (long)command.ExecuteScalar();
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

        public IEnumerable<Meta> GetPostMeta(IConnection connection, string meta_key)
        {
            var sql = new StringBuilder();

            var metas = new List<Meta>();
            foreach (var postMetaTable in PostMetasTables)
            {
                sql.AppendLine($"SELECT meta_id, post_id, meta_key, meta_value FROM {postMetaTable} WHERE meta_key = '{meta_key}';");
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
                            PostId = reader.GetUInt64("post_id"),
                            MetaKey = reader.GetString("meta_key"),
                            MetaValue = reader.GetString("meta_value")
                        };

                        metas.Add(meta);
                    }
                }
            }
            return metas;
        }

        #endregion

        #region IOptionRepository


        public void UpdateOptions(IConnection connection, string optionValue, string option_name)
        {
            var command = new MySqlCommand(string.Empty, connection.GetMySqlConnection());

            var escapedValue = MySqlHelper.EscapeString(optionValue);
            var escapedName = MySqlHelper.EscapeString(option_name);
            var sql = $"UPDATE {OptionTable} SET option_value = '{optionValue}' WHERE option_name = '{option_name}';";

            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        public Options GetOptionSetting(IConnection connection, string optionName)
        {
            var sql = $"SELECT option_name, option_value, autoload from  {OptionTable} where option_name = @optionName;";
            var command = new MySqlCommand(sql, connection.GetMySqlConnection());

            command.Parameters.Add(new MySqlParameter
            {
                ParameterName = "@optionName",
                MySqlDbType = MySqlDbType.VarString,
                Value = optionName
            });

            using (var reader = command.ExecuteReader())
            {
                var option = new Options();
                while (reader.Read())
                {
                    option.OptionName = reader.GetString("option_name");
                    option.OptionValue = reader.GetString("option_value");
                    option.Autoload = reader.GetString("autoload");
                }

                return option;
            }
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