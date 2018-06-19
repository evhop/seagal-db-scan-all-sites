using Fallback_blogg.WPClient.Model;
using Fallback_blogg.WPClient.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Fallback_blogg.Core;
using Fallback_blogg.Model;
using System.Net;
using System.Threading.Tasks;

namespace Fallback_blogg.Analys
{
    public class HttpRewrites : ISourceRewrites
    {
        public string Name => "http";
        private static Regex UrlHttpRegex = new Regex($"src=[\"'](http:.+?)[\"']", RegexOptions.Compiled);
        private List<HttpLink> _imageAnalysList;
        private Serializer _serializer = new Serializer();

        public HttpRewrites(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
        }

        public HttpRewrites(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        public void Execute(Context context, string time)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            _imageAnalysList = new List<HttpLink>();
            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    //Fixa länkar där det finns en https domän, de finns i appsettings under rewriteUrlToHttps
                    UpdateDomainHttpToHttps(context, client, connection);
                    ExecuteTransaction(context, client, connection);
                }
            }

            if (_imageAnalysList.Any())
            {
                var path = @"C:\Users\evhop\Dokument\dumps\Https";
                var pathFail = path + $"_fail_{time}.txt";
                var pathSuccess = path + $"_success_{time}.txt";
                //Skriv ut filen
                using (var failStream = File.AppendText(pathFail))
                {
                    using (var successStream = File.AppendText(pathSuccess))
                    {
                        foreach (var x in _imageAnalysList)
                        {
                            var logText = $"{x.SchemaTable}\t{x.Id}\t{x.HttpSource}\t{x.Succeded}";
                            if (x.Succeded == true)
                            {
                                successStream.WriteLine(logText);
                            }
                            else
                            {
                                failStream.WriteLine(logText);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateDomainHttpToHttps(Context context, IWPClient client, IConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var replaceFrom in context.Settings.RewriteUrlToHttps)
                {
                    string replaceTo = replaceFrom.Replace("http://", "https://");
                    client.UpdatePosts(connection, replaceFrom, replaceTo);
                    client.UpdatePostMetas(connection, replaceFrom, replaceTo);
                    client.UpdateComments(connection, replaceFrom, replaceTo);
                    client.UpdateCommentMetas(connection, replaceFrom, replaceTo);
                }

                //Avsluta transactionen
                transaction.Commit();
                transaction.Dispose();
            }
        }

        private void ExecuteTransaction(IContext context, IWPClient client, IConnection connection)
        {
            IEnumerable<Post> postContents;
            IEnumerable<Post> postExcerpts;
            IEnumerable<Post> postContentFiltereds;
            IEnumerable<Meta> postMetas;
            IEnumerable<Comment> comments;
            IEnumerable<Meta> commentMetas;

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var httpSearch = "src=\"http://";
                    //Hämta länkar
                    postContents = client.GetPosts(connection, "post_content", httpSearch);
                    postExcerpts = client.GetPosts(connection, "post_excerpt", httpSearch);
                    postContentFiltereds = client.GetPosts(connection, "post_content_filtered", httpSearch);
                    postMetas = client.GetPostMeta(connection, httpSearch);
                    comments = client.GetComments(connection, httpSearch);
                    commentMetas = client.GetCommentMeta(connection, httpSearch);

                    //Avsluta transactionen
                    transaction.Commit();
                    transaction.Dispose();
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    transaction.Rollback();
                    transaction.Dispose();
                    throw;
                }
            }

            GetHttpForPost(postContents);
            GetHttpForPost(postExcerpts);
            GetHttpForPost(postContentFiltereds);
            GetHttpForMeta(postMetas);
            GetHttpForComment(comments);
            GetHttpForMeta(commentMetas);
        }

        private void GetHttpForPost(IEnumerable<Post> posts)
        {
            if (!posts.Any())
            {
                return;
            }

            foreach (var post in posts)
            {
                GetLinkAsync(post.Id, post.SchemaTable, post.Content).Wait();
            }
        }

        private void GetHttpForComment(IEnumerable<Comment> comments)
        {
            if (!comments.Any())
            {
                return;
            }

            foreach (var comment in comments)
            {
                GetLinkAsync(comment.Id, comment.SchemaTable, comment.Content).Wait();
            }
        }

        private void GetHttpForMeta(IEnumerable<Meta> postMetas)
        {
            if (!postMetas.Any())
            {
                return;
            }

            MetaUrlRewriter metaUrlRewriter = new MetaUrlRewriter();
            foreach (var postMeta in postMetas)
            {
                var data = _serializer.Deserialize(postMeta.MetaValue);
                data = metaUrlRewriter.RewriteUrl(data);
                postMeta.MetaValue = _serializer.Serialize(data);
                //TODO lägga till i listan
            }
        }


        private async Task GetLinkAsync(ulong id, string schemaTable, string content)
        {
            var matches = UrlHttpRegex.Matches(content).ToList();

            if (!matches.Any())
            {
                return;
            }

            // Create a query.   
            IEnumerable<Task<HttpLink>> downloadTasksQuery =
                from match in matches
                where match.Success
                select ProcessURLAsync(match.Groups[1].Value, id, schemaTable);

            // Use ToArray to execute the query and start the download tasks.  
            Task<HttpLink>[] downloadTasks = downloadTasksQuery.ToArray();

            // Await the completion of all the running tasks.  
            HttpLink[] httpLinks = await Task.WhenAll(downloadTasks);
            _imageAnalysList.AddRange(httpLinks.ToList());
        }

        private async Task<HttpLink> ProcessURLAsync(string url, ulong id, string schemaTable)
        {
            var src = url;
            var srcHttps = src.Replace("http", "https");

            var httpLink = new HttpLink
            {
                SchemaTable = schemaTable,
                Id = id,
                HttpSource = src
            };
            /*
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(srcHttps);
                request.Method = "HEAD";
                var response = await request.GetResponseAsync();
                httpLink.HttpSource = srcHttps;
                //finns som https
                httpLink.Succeded = true;
            }
            catch (Exception e)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(src);
                    request.Method = "HEAD";
                    var response = await request.GetResponseAsync();
                    //finns som http men inte som https
                    httpLink.Succeded = false;
                }
                catch (Exception ex)
                {
                    //finns inte som http
                    httpLink.Succeded = null;
                }
            }
            */
            return httpLink;
        }
    }
}
