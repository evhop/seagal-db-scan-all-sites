using Seagal_TransformHttpContentToHttps.WPClient.Model;
using Seagal_TransformHttpContentToHttps.WPClient.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seagal_TransformHttpContentToHttps.Core;
using Seagal_TransformHttpContentToHttps.Model;
using Seagal_TransformHttpContentToHttps.WPClient;
using System.Net;
using System.Threading.Tasks;

namespace Seagal_TransformHttpContentToHttps.Analys
{
    public class SourceRewrites : ISourceRewrites
    {
        public string Name => "img-src";
        private static Regex ImageRegex = new Regex(@"<img>*", RegexOptions.Compiled);
        private static Regex UrlHttpRegex = new Regex($"src=[\"'](.+?)[\"'].+?", RegexOptions.Compiled);

        private List<HttpLink> _imageAnalysList = new List<HttpLink>();
        private Serializer _serializer = new Serializer();

        public SourceRewrites(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
        }

        public SourceRewrites(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        public void Execute(Context context)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            var time = DateTime.Now.ToString("yyyyMMddHHmmss");
            var pathFail = @"C:\Users\evhop\Dokument\dumps\Https.txt".Replace(".txt", $"_{time}_fail.txt");
            var pathSuccess = @"C:\Users\evhop\Dokument\dumps\Https.txt".Replace(".txt", $"_{time}_success.txt");

            if (File.Exists(pathFail))
            {
                File.Delete(pathFail);
            }

            if (File.Exists(pathSuccess))
            {
                File.Delete(pathSuccess);
            }

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    ExecuteTransaction(context, client, connection);
                }
            }
            //Skriv ut filen
            var distinctList = _imageAnalysList.Distinct().ToList();
            using (var failStream = File.AppendText(pathFail))
            {
                using (var successStream = File.AppendText(pathSuccess))
                {
                    foreach (var x in distinctList)
                    {
                        var logText = $"{x.SchemaTable}\t{x.Id}\t{x.HttpSource}";
                        if (x.Succeded == null)
                        {
                            //Hoppa över
                        }
                        else if (x.Succeded == true)
                        {
                            successStream.WriteLine(logText);
                        }
                        else
                        {
                            //Logger.LogInformation(TransformLog(logText));
                            failStream.WriteLine(logText);
                        }
                    }
                }
            }
        }

        private void ExecuteTransaction(IContext context, IWPClient client, IConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    //Hämta länkar för Post
                    var posts = client.GetPosts(connection);
                    //Avsluta transactionen
                    transaction.Commit();
                    if (posts.Any())
                    {
                        GetHttpForPost(posts);
                    }

                    //TODO Hämta länkar för Postmeta, commentmeta, comments, users, usermeta
                    //var metas = client.GetPostMeta(connection);
                    //if (metas.Any())
                    //{
                    //    GetHttpForPostmeta(metas, site);
                    //}

                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void GetHttpForPost(IEnumerable<Post> posts)
        {
            foreach (var post in posts)
            {
                GetLinkAsync(post.Id, post.SchemaTable, post.Content).Wait();
                GetLinkAsync(post.Id, post.SchemaTable, post.ContentFiltered).Wait();
                GetLinkAsync(post.Id, post.SchemaTable, post.Excerpt).Wait();
                //Guid ska inte hämtas då det bara är källor som hämtar in content som behöver skrivas om
            }
        }

        private async Task GetLinkAsync(ulong id, string schemaTable, string content)
        {
            var matches = UrlHttpRegex.Matches(content).ToList();

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
            return httpLink;
        }

        private void GetHttpForPostmeta(IEnumerable<Meta> postMetas, ISite site)
        {
            MetaUrlRewriter metaUrlRewriter = new MetaUrlRewriter(site);
            foreach (var postMeta in postMetas)
            {
                var data = _serializer.Deserialize(postMeta.MetaValue);
                data = metaUrlRewriter.RewriteUrl(data);
                postMeta.MetaValue = _serializer.Serialize(data);
                //TODO lägga till i listan
            }

        }
    }
}
