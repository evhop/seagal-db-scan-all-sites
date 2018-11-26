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
namespace WPDatabaseWork.Analys
{
    public class AomRewrites : ISourceRewrites
    {
        public string Name => "aom";
        private static Regex SrcRecipeidRegex = new Regex($"recipeid=", RegexOptions.Compiled);
        private Serializer _serializer = new Serializer();
        private IEnumerable<Post> _postContents;
        private IEnumerable<Post> _postRecipeLinks;

        public AomRewrites(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
        }

        public AomRewrites(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        #region Execute
        public void Execute(Context context, string time)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    _postRecipeLinks = client.GetRecipeLinks(connection);
                    _postContents = client.GetPostsRegexp(connection, "post_content", SrcRecipeidRegex.ToString());
                    List<Post> changeContent = new List<Post>();

                    foreach (var recipeLink in _postRecipeLinks)
                    {
                        var postWithLink = _postContents.Where(x => x.Content.Contains($"\"{recipeLink.Content}\"")).ToList();
                        foreach (var post in postWithLink)
                        {
                            Post newLink = new Post
                            {
                                Id = post.Id,
                                SchemaTable = post.SchemaTable,
                                Date = post.Date,
                                Content = $"\"{recipeLink.OldContent}\"",
                                OldContent = $"\"{recipeLink.Content}\""
                            };
                            changeContent.Add(newLink);
                        }
                    }
                    client.CreateSqlReplaceUpdatePostsfile(connection, changeContent, "post_content", @"C:\Users\evhop\Dokument\dumps\aom_recipeId", DateTime.Now.ToShortDateString());
                }
            }
        }

        #endregion

        public void WriteUrlToFile(string path)
        {
            throw new NotImplementedException();
        }

        public void ExecuteUpdate(Context context)
        {
            throw new NotImplementedException();
        }
    }
}
