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
    public class FindSrcBMB : ISourceRewrites
    {
        public string Name => "srcBMB";
        private static Regex SrcUrlHttpRegex = new Regex($"src=[\"']((http(s)?://[^/]+)?(/(?!/)[^\"']+))", RegexOptions.Compiled);
        private static Regex DomainHttpRegex = new Regex($"src=[\"](http(s)?://[^/\"]+)", RegexOptions.Compiled);

        private List<HttpLink> _analysList;
        private IEnumerable<Post> _postContents;
        private IContext _context;
        private HtmlParser _htmlParser;
        private List<Post> _postReplacedContents;
        private string[] sites = new string[] {  "alltommat",  "alltomtradgard",  "viforaldrar",  "veckorevyn",
                                    "topphalsa",  "tidningenhembakat",  "teknikensvarld",  "tara",
                                    "styleby",  "skonahem",  "m-magasin",  "mama",  "lantliv",
                                    "kpwebben",  "godsochgardar",  "gardochtorp",  "dvmode.",
                                    "damernasvarld",  "amelia", 
                                    "alltihemmet",  "hemochantik",  "familyliving",  "bontravel",
                                    "grid-cms.bonnierdigitalservices"};

        public FindSrcBMB(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
            _analysList = new List<HttpLink>();
        }

        public FindSrcBMB(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        #region Execute
        //Fixa till bildlänkar så att de går mot blobb för andra BMBsiter
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
            string[] httpPrefixs = new string[] { "http://", "https://" };
            string[] wwwPrefixs = new string[] { "", "www." };
            Dictionary<string, string> bmbSites = new Dictionary<string, string>
            {
                { "alltommat.se", "aom"},
                { "alltomtradgard.se", "aot" },
                { "viforaldrar.se", "vif" },
                { "veckorevyn.com", "vr" },
                { "topphalsa.se", "top" },
                { "tidningenhembakat.se", "bak"},
                { "teknikensvarld.se", "tv" },
                { "tara.se", "t" },
                { "styleby.nu", "sb" },
                { "skonahem.com", "skh" },
                { "m-magasin.se", "mm" },
                { "mama.nu", "mam" },
                { "lantliv.com", "ll" },
                { "kpwebben.se", "kp" },
                { "godsochgardar.se", "gog" },
                { "gardochtorp.se", "got" },
                { "dvmode.se", "dv" },
                { "damernasvarld.se", "dv" },
                { "amelia.se", "ame" },
                { "alltihemmet.se", "aih" },
                { "hemochantik.se", "hoa" },
                { "familyliving.se", "fl" },
                { "bontravel.se", "btr" }
            };

            _context = context;
            _postReplacedContents = new List<Post>();

            var clientFactory = _context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = _context.Settings;

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    if (_context.Options.BloggId != null)
                    {
                        client.GetTableSchema(connection, settings.DestinationDb.Schema, _context.Options.BloggId);
                    }
                    else
                    {
                        client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    }

                    foreach (var httpPrefix in httpPrefixs)
                    {
                        foreach (var wwwPrefix in wwwPrefixs)
                        {
                            foreach (var bmbSite in bmbSites)
                            {
                                string filePath = httpPrefix + wwwPrefix + bmbSite.Key + "/wp-content/uploads/";
                                string blobPath = "https://" + bmbSite.Value + "eassetsprod.blob.core.windows.net/editorial/";

                                Console.WriteLine($"Process {filePath}, {DateTime.Now.ToShortTimeString()} ");
                                using (var transaction = connection.BeginTransaction())
                                {
                                    try
                                    {
                                        client.UpdatePosts(connection, filePath, blobPath);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.Write(e.Message);
                                        transaction.Rollback();
                                        transaction.Dispose();
                                        throw;
                                    }
                                    finally
                                    {
                                        transaction.Commit();
                                    }
                                }
                            }
                        }
                    }
                }
            }
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
                    if (_context.Options.BloggId != null)
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
                            if (!String.IsNullOrEmpty(_context.Options.Year))
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
            double processInterval = 50;

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
            Console.WriteLine($"For {_context.Settings.DestinationDb.Schema}, total {_analysList.Count}");
        }

        private async Task GetLinkAsync(Post post, int urlGroup)
        {
            var matches = SrcUrlHttpRegex.Matches(post.Content).ToList();

            if (!matches.Any())
            {
                return;
            }

            foreach (var match in matches.Where(m => m.Success).ToList())
            {
                string sourceUrl = match.Groups[urlGroup].Value;

                //Tar endast med de som innehåller någon av sitenamnen
                foreach (var site in sites)
                {
                    if (sourceUrl.Contains(site + ".") && !sourceUrl.Contains("assetsprod.blob") && !sourceUrl.Contains("ppadmin.btdmtech"))
                    {
                        HttpLink httpLink = new HttpLink
                        {
                            HttpSource = sourceUrl,
                            HttpsSource = sourceUrl,
                            Succeded = false,
                            Id = post.Id,
                            Guid = post.Guid,
                            Date = post.Date,
                            SchemaTable = post.SchemaTable
                        };
                        _analysList.Add(httpLink);
                        break;
                    }
                }
            }
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
                                var logText = $"{x.SchemaTable}\t{x.Id}\t{x.HttpSource}\t{x.Guid}";
                                successStream.WriteLine(logText);
                            }
                            else
                            {
                                var logText = $"{x.SchemaTable}\t{x.Id}\t{x.HttpSource}\t{x.Guid}";
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
