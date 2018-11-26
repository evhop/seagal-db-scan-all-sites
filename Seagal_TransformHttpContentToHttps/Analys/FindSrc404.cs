using WPDatabaseWork.WPClient.Model;
using WPDatabaseWork.WPClient.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WPDatabaseWork.Core;
using WPDatabaseWork.Model;
using System.Net;
using System.Threading.Tasks;

namespace WPDatabaseWork.Analys
{
    public class FindSrc404 : ISourceRewrites
    {
        public string Name => "src404";
        private static Regex SrcUrlHttpRegex = new Regex($"src=[\"']((http(s)?://[^/]+)?(/(?!/)[^\"']+))", RegexOptions.Compiled);
        private static Regex DomainHttpRegex = new Regex($"src=[\"](http(s)?://[^/\"]+)", RegexOptions.Compiled);

        private List<HttpLink> _analysList;
        private IEnumerable<Post> _postContents;
        private IContext _context;
        private HtmlParser _htmlParser;
        private List<Post> _postReplacedContents;


        public FindSrc404(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
            _analysList = new List<HttpLink>();
        }

        public FindSrc404(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        #region Execute
        //Fixa länkar där det finns en https domän, de finns i appsettings under rewriteUrlToHttps
        public void Execute(Context context, string time)
        {
            _context = context;
            _htmlParser = new HtmlParser(_context);
            _postReplacedContents = new List<Post>();

            GetWPHttpLinks();
            GetHttpForPost(1);
        }

        public void ExecuteUpdate(Context context)
        {
            throw new NotImplementedException();
        }

        public void ExecuteAllHttpLinks(Context context, string time)
        {
            throw new NotImplementedException();
        }

        private void GetWPHttpLinks()
        {
            var clientFactory = _context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = _context.Settings;

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    if (_context.Options.BloggId == null)
                    {
                        client.GetTableSchema(connection, settings.DestinationDb.Schema, _context.Options.BloggId);
                    }
                    else
                    {
                        client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            //Hämta länkar
                            _postContents = client.GetPostsRegexp(connection, "post_content", DomainHttpRegex.ToString());

                            //Bearbetar endast ett visst år om det är angivet 
                            if (_context.Options.Year != "")
                            {
                                _postContents = _postContents.Where(x => x.Date.Contains(_context.Options.Year)).ToList();
                            }

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

        private void GetHttpForPost(int urlGroup)
        {
            int count = 0;
            double processStart = 30;
            double processStop = 100;
            double processInterval = 10;

            if (!_postContents.Any())
            {
                return;
            }

            Console.WriteLine($"Processing {count} of {_postContents.Count()} posts");
            foreach (var post in _postContents)
            {
                GetLinkAsync(post, urlGroup).Wait();

                count++;
                if (count % processInterval == 0)
                {
                    int processStep = (int)Math.Round((double)(100 * count) / _postContents.Count());
                    processStart += processStep;
                    Console.WriteLine($"Processed {count} of {_postContents.Count()} posts");
                }
            }
        }

        private async Task GetLinkAsync(Post post, int urlGroup)
        {
            var matches = SrcUrlHttpRegex.Matches(post.Content).ToList();

            if (!matches.Any())
            {
                return;
            }

            List<Match> newMatches = new List<Match>();

            foreach (var match in matches.Where(m => m.Success).ToList())
            {
                string sourceUrl = match.Groups[urlGroup].Value;
                bool urlExists = (_analysList.Exists(m => m.HttpSource == sourceUrl) ||
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
                select ProcessURLAsync(match, post.Id, post.Date, post.SchemaTable);

            // Use ToArray to execute the query and start the download tasks.  
            Task<HttpLink>[] downloadTasks = downloadTasksQuery.ToArray();

            // Await the completion of all the running tasks.  
            HttpLink[] httpLinks = await Task.WhenAll(downloadTasks);

            if (_context.Options.UpdateUrlFromImageId)
            {
                if (httpLinks.ToList().Exists(x => x.Succeded == false))
                {
                    _postReplacedContents.AddRange(_htmlParser.replaceUrl(httpLinks.Where(x => x.Succeded == false).ToList(), post, false));
                }
            }
            _analysList.AddRange(httpLinks.ToList());
        }

        private async Task<HttpLink> ProcessURLAsync(Match match, ulong id, string date, string schemaTable)
        {
            var src = match.Groups[1].Value;
            var httpLink = new HttpLink
            {
                SchemaTable = schemaTable,
                Id = id,
                Date = date,
                HttpSource = match.Groups[1].Value
            };

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(src);
                var response = await request.GetResponseAsync();
                //finns som https
                var responseUri = response.ResponseUri.ToString();
                httpLink.HttpsSource = httpLink.HttpSource.Replace(src, responseUri.TrimEnd('/'));
                httpLink.Succeded = true;
            }
            catch (WebException we)
            {
                //finns inte
                httpLink.Succeded = false;
            }
            catch (Exception e)
            {
                //finns inte
                httpLink.Succeded = false;
            }
            return httpLink;
        }

        #endregion

        #region helper
        public void WriteUrlToFile(string path)
        {
            if (_analysList.Any())
            {
                var pathFail = path + $"_fail.txt";
                var pathSuccess = path + $"_success.txt";
                //Skriv ut filen
                using (var failStream = File.AppendText(pathFail))
                {
                    using (var successStream = File.AppendText(pathSuccess))
                    {
                        foreach (var x in _analysList)
                        {
                            if (x.Succeded == true)
                            {
                                var logText = $"{x.SchemaTable}\t{x.Id}\t{x.HttpSource}";
                                successStream.WriteLine(logText);
                            }
                            else
                            {
                                var logText = $"{x.SchemaTable}\t{x.Id}\t{x.HttpSource}";
                                failStream.WriteLine(logText);
                            }
                        }
                    }
                }
            }

            //skriver ut updatesql
            if (_postReplacedContents.Any())
            {
                var clientFactory = _context.ServiceProvider.GetService<IWPClientFactory>();
                var settings = _context.Settings;

                using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
                {
                    using (var connection = client.CreateConnection())
                    {
                        client.GetTableSchema(connection, settings.DestinationDb.Schema);
                        using (var transaction = connection.BeginTransaction())
                        {
                            client.CreateSqlReplaceUpdatePostsfile(connection, _postReplacedContents, "post_content", path, "");
                        }
                    }
                }
            }
        }
        #endregion
    }
}
