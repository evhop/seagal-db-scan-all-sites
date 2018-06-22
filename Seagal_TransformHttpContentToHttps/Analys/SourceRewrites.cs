using Fallback_blogg.WPClient.Model;
using Fallback_blogg.WPClient.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Fallback_blogg.Core;
using Fallback_blogg.Model;

namespace Fallback_blogg.Analys
{
    public class SourceRewrites : ISourceRewrites
    {
        public string Name => "img-src";
        private static Regex UrlHttpRegex = new Regex($"(=)((http(s)?:?)?//[A-Za-z\\./0-9_\\-ÅÄÖåäö]+[^\"])([> ])", RegexOptions.Compiled);
        private static Regex UrlSlachRegex = new Regex($"=(//[A-Za-z\\./0-9_\\-ÅÄÖåäö]+[^\"])([> ])", RegexOptions.Compiled);

        public SourceRewrites(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
        }

        public SourceRewrites(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        public void ExecuteAllHttpLinks(Context context, string time)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    ExecuteTransaction(context, client, connection, time);
                }
            }
        }

        private void ExecuteTransaction(IContext context, IWPClient client, IConnection connection, string time)
        {
            IEnumerable<Post> posts;
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    //Hämta länkar
                    posts = client.GetPosts(connection, "post_content");

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

            var updatedPosts = GetHttpForPost(posts);
            if (updatedPosts.Any())
            {
                try
                {
                    var path = @"C:\Users\evhop\Dokument\dumps\Fallback";
                    //client.UpdatePosts(connection, updatedPosts, "post_content");
                    client.CreateSqlUpdatePostsfile(connection, updatedPosts, "post_content", path, time);
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    throw;
                }
            }
        }

        private IEnumerable<Post> GetHttpForPost(IEnumerable<Post> posts)
        {
            List<Post> updatePosts = new List<Post>();
            foreach (var post in posts)
            {
                var replaceContent = ReplaceUrls(post.Content);
                if (!string.Equals(post.Content, replaceContent))
                {
                    post.Content = replaceContent;
                    updatePosts.Add(post);
                }

            }
            return updatePosts;
        }

        private string ReplaceUrls(string text)
        {
            if (!String.IsNullOrEmpty(text))
            {
                var textReplaced = UrlHttpRegex.Replace(text, match => RewriteSource(match));
                if (!String.Equals(text, textReplaced))
                {
                    text = textReplaced;
                }
            }
            return text;
        }

        private string RewriteSource(Match match)
        {
            var group1 = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            var group2 = match.Groups[5].Value;
            return group1 + "\"" + url + "\"" + group2;
        }

        public void ExecuteUpdateDomain(Context context)
        {
            throw new NotImplementedException();
        }

        public void ExecuteGetDomain(Context context, string time)
        {
            throw new NotImplementedException();
        }

        public void WriteUrlToFile(string path, string time)
        {
            throw new NotImplementedException();
        }
    }
}
