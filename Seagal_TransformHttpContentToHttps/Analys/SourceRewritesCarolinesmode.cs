using WPDatabaseWork.WPClient.Model;
using WPDatabaseWork.WPClient.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WPDatabaseWork.Core;
using WPDatabaseWork.Model;

namespace WPDatabaseWork.Analys
{
    public class SourceRewritesCarolinesMode : ISourceRewrites
    {
        public string Name => "caro";
        private static Regex ImageRegex = new Regex($"/[0-9]+[\\-0-9]+?[\\.a-zA-Z]+", RegexOptions.Compiled);
        private static Regex UrlRegex = new Regex("<img\\s+[^>]*?src=\"([^\"\\r\\n<>]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private List<Post> _attachments = new List<Post>();
        private List<Meta> _metas = new List<Meta>();

        public SourceRewritesCarolinesMode(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
        }

        public SourceRewritesCarolinesMode(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        public void Execute(Context context, string time)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema, "243");
                    ExecuteTransaction(context, client, connection, time);
                }
            }
        }

        private void ExecuteTransaction(IContext context, IWPClient client, IConnection connection, string time)
        {
            List<Post> posts;

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    //Hämta länkar
                    posts = client.GetPosts(connection, "post_content","%/feber/%", 5000).ToList();
                    _attachments = client.GetAttachments(connection).ToList();
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

            var updatedPosts = GetImageForPost(posts);
            if (updatedPosts.Any())
            {
                try
                {
                    client.UpdatePosts(connection, updatedPosts, "post_content");
                    client.InsertPostMetas(connection, _metas);
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    throw;
                }
            }
        }

        private IEnumerable<Post> GetImageForPost(List<Post> posts)
        {
            List<Post> updatePosts = new List<Post>();
            foreach (var post in posts)
            {
                ReplaceUrls(post);
                updatePosts.Add(post);
            }
            return updatePosts;
        }

        private void ReplaceUrls(Post post)
        {
            if (!String.IsNullOrEmpty(post.Content))
            {
                var matches = UrlRegex.Matches(post.Content);
                foreach(Match match in matches)
                {
                    try
                    {
                        var image = ImageRegex.Match(match.ToString()).ToString().Replace("/", "");
                        string textNew = "https://dvbassetsstage.blob.core.windows.net/243/2015/04/" + image.ToString();
                        post.Content = post.Content.Replace(match.Groups[1].ToString(), textNew);

                        //Bara första bilden ska läggas in som thumbnail på posten
                        bool metaExists = _metas.Exists(x => x.PostId == post.Id);
                        if (!metaExists)
                        {
                            string thumbnailId = _attachments.Find(x => x.OldContent == image).Id.ToString();
                            _metas.Add(new Meta
                            {
                                SchemaTable = post.SchemaTable.Replace("posts", "postmeta"),
                                PostId = post.Id,
                                MetaKey = "_thumbnail_id",
                                MetaValue = thumbnailId
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Write(post.Id.ToString() + " " + post.Content + " " + e);
                    }
                }
            }
        }

        public void ExecuteUpdate(Context context)
        {
            throw new NotImplementedException();
        }

        public void WriteUrlToFile(string path)
        {
            throw new NotImplementedException();
        }
    }
}
