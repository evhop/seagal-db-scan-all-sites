using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using WPDatabaseWork.Core;
using WPDatabaseWork.WPClient.Model;
using WPDatabaseWork.WPClient.View;
using Microsoft.Extensions.DependencyInjection;
using WPDatabaseWork.Model;
using System.Linq;

namespace WPDatabaseWork.Analys
{
    public class RestoreMedia : ISourceRewrites
    {
        public string Name => "restoreMedia";
        private IEnumerable<Post> _postContents;
        private List<Attachment> _insertAttachments;
        private List<Post> _updateAttachments;

        public RestoreMedia(ILoggerFactory loggerFactory)
    : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
        }

        public RestoreMedia(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        public void Execute(Context context, string time)
        {
            GetWPPostWithNoAttachment(context);
            GetImageForPost(context);
            InsertAttachments(context);
            UpdateAttachments(context);
        }

        private void GetWPPostWithNoAttachment(IContext context)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;
            string likeSearch = "%src=\"https://tsbas%";

            using (var client = clientFactory.CreateClient(settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema, context.Options.BloggId);
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            //Hämta länkar
                            _postContents = client.GetPostsWithImagesAndNoAttachments(connection, likeSearch);

                            //Bearbetar endast ett visst år om det är angivet 
                            if (context.Options.Year != "")
                            {
                                _postContents = _postContents.Where(x => x.Date.Contains(context.Options.Year)).ToList();
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

        private void GetImageForPost(IContext context)
        {
            _insertAttachments = new List<Attachment>();
            _updateAttachments = new List<Post>();
            HtmlParser htmlParser = new HtmlParser(context);

            foreach (var post in _postContents)
            {
                List<Attachment> replaceContent = htmlParser.ImagesTailsweep(post);
                _insertAttachments.AddRange(replaceContent);
                _updateAttachments.AddRange(htmlParser._updateParentIds);
            }
        }

        private void InsertAttachments(IContext context)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            //Lägger till posten
            using (var client = clientFactory.CreateClient(context.Settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, context.Settings.DestinationDb.Schema, context.Options.BloggId);
                    client.InsertPosts(connection, _insertAttachments);
                }
            }
        }

        private void UpdateAttachments(IContext context)
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            //Lägger till posten
            using (var client = clientFactory.CreateClient(context.Settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, context.Settings.DestinationDb.Schema, context.Options.BloggId);
                    client.UpdateParentIds(connection, _updateAttachments);
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
