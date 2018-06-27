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
/* Todo
 * Scana igenom alla databaser för att ta reda på vilka domäner som finns som https
 * Uppdatera alla domäner som finns som https i alla databaser
 * 
 * Redan kända från tidigare databas
 * Nya från nuvarande databas
 * 
 * Lösning
 * En lista med domäner för alla databaser
 * update nuvarande databas
 * skriv ut de som finns som http men inte som https
 * skriv ut de som inte finns alls (404)
 */
namespace Fallback_blogg.Analys
{
    public class HttpRewrites : ISourceRewrites
    {
        public string Name => "http";
        private static Regex SrcUrlHttpRegex = new Regex($"src=[\"']((http://[^/]+)?(/(?!/)[^\"']+))", RegexOptions.Compiled);
        private static Regex DomainHttpRegex = new Regex($"src=[\"](http://[^/\"]+)", RegexOptions.Compiled);
        private List<HttpLink> _httpAnalysList;
        private Serializer _serializer = new Serializer();
        private IEnumerable<Post> _postContents;
        private IEnumerable<Post> _postExcerpts;
        private IEnumerable<Post> _postContentFiltereds;
        private IEnumerable<Post> _comments;
        private IEnumerable<Meta> _postMetas;
        private IEnumerable<Meta> _commentMetas;

        public HttpRewrites(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
            _httpAnalysList = new List<HttpLink>();
        }

        public HttpRewrites(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        #region Execute
        //Fixa länkar där det finns en https domän, de finns i appsettings under rewriteUrlToHttps
        public void ExecuteUpdateDomain(Context context)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    UpdateWPDomainHttpToHttps(context, client, connection);
                }
            }
        }

        public void ExecuteGetDomain(Context context, string time)
        {
            GetWPHttpLinks(context);

            GetHttpForPost(_postContents, 0);
            GetHttpForPost(_postExcerpts, 0);
            GetHttpForPost(_postContentFiltereds, 0);
            GetHttpForComment(_comments, 0);

            List<HttpLink> httpsLinks = new List<HttpLink>();
            List<HttpLink> httpLinks = new List<HttpLink>();
            List<HttpLink> httpLinksRest = new List<HttpLink>();
            GetHttpsLinks(_postContents, httpsLinks, httpLinks,httpLinksRest);
            GetHttpsLinks(_postExcerpts, httpsLinks, httpLinks, httpLinksRest);
            GetHttpsLinks(_postContentFiltereds, httpsLinks, httpLinks, httpLinksRest);
            GetHttpsLinks(_comments, httpsLinks, httpLinks, httpLinksRest);
            WriteUrlToFile(@"C:\Users\evhop\Dokument\dumps\Http_" + $"{time}_success.txt", httpsLinks);
            WriteUrlToFile(@"C:\Users\evhop\Dokument\dumps\Http_" + $"{time}_onlyhttp.txt", httpLinks);
            WriteUrlToFile(@"C:\Users\evhop\Dokument\dumps\Http_" + $"{time}_404.txt", httpLinksRest);
        }

        private void GetHttpsLinks(IEnumerable<Post> postContents, List<HttpLink> httpsLinks, List<HttpLink> httpLinks, List<HttpLink> httpLinksRest)
        {
            //var matches = SrcUrlHttpRegex.Matches(content).ToList();
            foreach (var httpDomain in _httpAnalysList)
            {
                var httpPost = (from post in postContents
                                where post.Content.Contains(httpDomain.HttpSource)
                                select post).ToList();
                foreach (var post in httpPost)
                {
                    var matches = SrcUrlHttpRegex.Matches(post.Content).Where(m => m.Success).ToList();
                    if (!matches.Any())
                    {
                        continue;
                    }
                    foreach (var match in matches)
                    {
                        var httpLink = new HttpLink
                        {
                            Id = post.Id,
                            SchemaTable = post.SchemaTable,
                            Date = post.Date,
                            HttpSource = match.Groups[0].Value,
                        };
                        if (httpDomain.Succeded == true && match.Groups[0].Value.Contains(httpDomain.HttpSource))
                        {
                            var replaceMatch = match.Groups[0].Value.Replace(httpDomain.HttpSource, httpDomain.HttpsSource);
                            httpLink.HttpsSource = replaceMatch;
                            httpsLinks.Add(httpLink);
                        }
                        else if (httpDomain.Succeded == false && match.Groups[0].Value.Contains(httpDomain.HttpSource))
                        {
                            httpLinks.Add(httpLink);
                        }
                        else if (match.Groups[0].Value.Contains(httpDomain.HttpSource))
                        {
                            httpLinksRest.Add(httpLink);
                        }
                    }
                }
            }
        }

        public void ExecuteAllHttpLinks(Context context, string time)
        {
            GetWPHttpLinks(context);

            GetHttpForPost(_postContents, 1);
            GetHttpForPost(_postExcerpts, 1);
            GetHttpForPost(_postContentFiltereds, 1);
            GetHttpForMeta(_postMetas, 1);
            GetHttpForComment(_comments, 1);
            GetHttpForMeta(_commentMetas, 1);
        }

        #endregion

        private void UpdateWPDomainHttpToHttps(Context context, IWPClient client, IConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var replaceFrom in context.Settings.RewriteUrlToHttps)
                {
                    string replaceTo = replaceFrom.Replace("http://", "https://");
                    client.UpdatePosts(connection, replaceFrom, replaceTo);
                    client.UpdateComments(connection, replaceFrom, replaceTo);

                    //TODO kan inte bara ändra för då blir längden fel
                    //client.UpdatePostMetas(connection, replaceFrom, replaceTo);
                    //client.UpdateCommentMetas(connection, replaceFrom, replaceTo);
                }

                //Avsluta transactionen
                transaction.Commit();
                transaction.Dispose();
            }
        }

        private void GetWPHttpLinks(IContext context)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            //Hämta länkar
                            _postContents = client.GetPosts(connection, "post_content", DomainHttpRegex.ToString());
                            _postExcerpts = client.GetPosts(connection, "post_excerpt", DomainHttpRegex.ToString());
                            _postContentFiltereds = client.GetPosts(connection, "post_content_filtered", DomainHttpRegex.ToString());
                            _comments = client.GetComments(connection, DomainHttpRegex.ToString());
                            //Behöver inte göras
                            //_postMetas = client.GetPostMeta(connection, DomainHttpRegex.ToString());
                            //_commentMetas = client.GetCommentMeta(connection, DomainHttpRegex.ToString());
                        }
                        catch (Exception e)
                        {
                            Console.Write(e.Message);
                            transaction.Rollback();
                            transaction.Dispose();
                            throw;
                        }
                    }
                }
            }
        }

        private void GetHttpForPost(IEnumerable<Post> posts, int urlGroup)
        {
            if (!posts.Any())
            {
                return;
            }

            foreach (var post in posts)
            {
                GetLinkAsync(post.Id, post.SchemaTable, post.Content,post.Date, urlGroup).Wait();
            }
        }

        private void GetHttpForComment(IEnumerable<Post> comments, int urlGroup)
        {
            if (!comments.Any())
            {
                return;
            }

            foreach (var comment in comments)
            {
                GetLinkAsync(comment.Id, comment.SchemaTable, comment.Content, comment.Date, urlGroup).Wait();
            }
        }

        private void GetHttpForMeta(IEnumerable<Meta> postMetas, int urlGroup)
        {
            if (!postMetas.Any())
            {
                return;
            }

            MetaUrlRewriter metaUrlRewriter = new MetaUrlRewriter();
            foreach (var postMeta in postMetas)
            {
                GetLinkAsync(postMeta.MetaId, postMeta.SchemaTable, postMeta.MetaValue, null, urlGroup).Wait();
                //var data = _serializer.Deserialize(postMeta.MetaValue);
                //data = metaUrlRewriter.RewriteUrl(data, urlGroup);
                //postMeta.MetaValue = _serializer.Serialize(data);
                //TODO lägga till i listan
            }
        }

        #region helper
        public void WriteUrlToFile(string path)
        {
            if (_httpAnalysList.Any())
            {
                var pathFail = path + $"_fail.txt";
                var pathSuccess = path + $"_success.txt";
                //Skriv ut filen
                using (var failStream = File.AppendText(pathFail))
                {
                    using (var successStream = File.AppendText(pathSuccess))
                    {
                        foreach (var x in _httpAnalysList)
                        {
                            if (x.Succeded == true)
                            {
                                var logText = $"{x.HttpSource}\t{x.HttpsSource}";
                                successStream.WriteLine(logText);
                            }
                            else
                            {
                                var logText = $"{x.SchemaTable}\t{x.Id}\t{x.HttpSource}\t{x.Succeded}";
                                failStream.WriteLine(logText);
                            }
                        }
                    }
                }
            }
        }

        private void WriteUrlToFile(string path, List<HttpLink> links)
        {
            if (links.Any())
            {
                //Skriv ut filen
                using (var successStream = File.AppendText(path))
                {
                    foreach (var x in links)
                    {
                        var logText = $"{x.SchemaTable}\t{x.Id}\t{x.Date}\t{x.HttpSource}\t{x.HttpsSource}";
                        successStream.WriteLine(logText);
                    }
                }
            }
        }

        private async Task GetLinkAsync(ulong id, string schemaTable, string content, string date, int urlGroup)
        {
            //var matches = SrcUrlHttpRegex.Matches(content).ToList();
            var matches = DomainHttpRegex.Matches(content).ToList();

            if (!matches.Any())
            {
                return;
            }

            List<Match> newMatches = new List<Match>();

            foreach (var match in matches.Where(m => m.Success).ToList())
            {
                string sourceUrl = match.Groups[urlGroup].Value;
                bool urlExists = (_httpAnalysList.Exists(m => m.HttpSource == sourceUrl) ||
                    newMatches.Exists(m => m.Groups[urlGroup].Value == sourceUrl));

                //Endast validera urlen om den inte redan finns i listan
                if (!urlExists)
                {
                    newMatches.Add(match);
                }
            }

            // Kontrollera om urlen finns som https   
            IEnumerable<Task<HttpLink>> downloadTasksQuery =
                from match in newMatches
                where match.Success
                select ProcessURLAsync(match, id, date, schemaTable);

            // Use ToArray to execute the query and start the download tasks.  
            Task<HttpLink>[] downloadTasks = downloadTasksQuery.ToArray();

            // Await the completion of all the running tasks.  
            HttpLink[] httpLinks = await Task.WhenAll(downloadTasks);
            _httpAnalysList.AddRange(httpLinks.ToList());
        }

        private async Task<HttpLink> ProcessURLAsync(Match match, ulong id, string date, string schemaTable)
        {
            var src = match.Groups[1].Value;
            var httpLink = new HttpLink
            {
                SchemaTable = schemaTable,
                Id = id,
                Date = date,
                HttpSource = match.Groups[0].Value
            };

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(src);
                var response = await request.GetResponseAsync();
                //finns som https
                var responseUri = response.ResponseUri.ToString();
                if (responseUri.StartsWith("https"))
                {
                    httpLink.HttpsSource = httpLink.HttpSource.Replace(src, responseUri.TrimEnd('/'));
                    httpLink.Succeded = true;
                }
                else
                {
                    httpLink.Succeded = false;
                }
            }
            catch (WebException we)
            {
                //finns inte som http
                httpLink.Succeded = null;
            }
            catch (Exception e)
            {
                //finns inte som http
                httpLink.Succeded = null;
            }
            return httpLink;
        }
        #endregion
    }
}
