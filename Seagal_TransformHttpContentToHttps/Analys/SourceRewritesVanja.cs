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
using WPDatabaseWork.View;

namespace WPDatabaseWork.Analys
{
    public class SourceRewritesVanja //: ISourceRewrites
    {
        public string Name => "vanja";
        private static Regex ImageRegex = new Regex($"/[0-9]+[\\-0-9]+?[\\.a-zA-Z]+", RegexOptions.Compiled);
        private static Regex UrlRegex = new Regex("<img\\s+[^>]*?src=\"([^\"\\r\\n<>]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private List<HttpLink> _httpAnalysList;
        private Serializer _serializer = new Serializer();
        private IEnumerable<Post> _postContents;
        private List<Post> _replaceContents;
        
        public SourceRewritesVanja(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
        }

        public SourceRewritesVanja(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        #region Execute
        public void ExecuteUpdate(Context context)
        {
            throw new NotImplementedException();
        }

        public void Execute(Context context, string time)
        {
            GetWPPost(context);
            GetImageForPost(context);
            WriteUrlToFile(context, @"C:\Users\evhop\Dokument\dumps\Vanja_", time);
        }

        #endregion

        private void GetWPPost(IContext context)
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
                            _postContents = client.GetPostsRegexp(connection, "post_content", "vanja\\.metromode");
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

        private void GetImageForPost(IContext context)
        {
            _replaceContents = new List<Post>();
            if (!_postContents.Any())
            {
                return;
            }

            HtmlParser htmlParser = new HtmlParser(context);

            foreach (var post in _postContents)
            {
                _replaceContents.AddRange(htmlParser.ImageVanja(post));
            }
        }

        #region helper
        public void WriteUrlToFile(string path)
        {
        }

        private void WriteUrlToFile(IContext context, string path, string time)
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
                        client.CreateSqlReplaceUpdatePostsfile(connection, _replaceContents, "post_content", path, time);
                    }
                }
            }
        }

        #endregion
    }
}
