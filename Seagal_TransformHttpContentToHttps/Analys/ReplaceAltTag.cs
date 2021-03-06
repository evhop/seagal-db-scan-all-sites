﻿using WPDatabaseWork.WPClient.Model;
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
/* Todo
 * Hitta alla img src som har alt=""
 * Hämta bildtiteln
 * 
 * Rensa bort _ och ev tab
 */
namespace WPDatabaseWork.Analys
{
    public class ReplaceAltTag : ISourceRewrites
    {
        public string Name => "alttag";
        private static Regex AltTagRegex = new Regex($"alt=\"\"", RegexOptions.Compiled);
        private List<HttpLink> _httpAnalysList;
        private IEnumerable<Post> _postContents;
        private List<ulong> _ImageNotExistsId;
        private List<Post> _replaceContents;

        public ReplaceAltTag(ILoggerFactory loggerFactory)
            : this(loggerFactory.CreateLogger<SourceRewrites>())
        {
            _ImageNotExistsId = new List<ulong>();
        }

        public ReplaceAltTag(ILogger logger) => Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private ILogger Logger { get; }

        #region Execute
        public void ExecuteUpdate(Context context)
        {
            throw new NotImplementedException();
        }

        public void Execute(Context context, string time)
        {
            GetWPAltTag(context);
            GetImageForPost(context);
            WriteUrlToFile(context, @"C:\Users\evhop\Dokument\dumps\TV_AltTag_", time);
            WriteUrlToFile(@"C:\Users\evhop\Dokument\dumps\TV_AltTag_ImageNotExists.txt");
        }

        #endregion

        private void GetWPAltTag(IContext context)
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
                            _postContents = client.GetPostsRegexp(connection, "post_content", AltTagRegex.ToString());
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
                List<Post> replaceContent = htmlParser.AltTagTV(post);
                foreach (var content in replaceContent)
                {
                    if (content.Content == "not exists")
                    {
                        _ImageNotExistsId.Add(content.Id);
                    }
                    else
                    {
                        _replaceContents.Add(content);
                    }
                }
            }
        }

        #region helper
        public void WriteUrlToFile(string path)
        {
            if (_ImageNotExistsId.Any())
            {
                //Skriv ut filen
                using (var successStream = File.AppendText(path))
                {
                    foreach (var x in _ImageNotExistsId)
                    {
                        var logText = $"{x}";
                        successStream.WriteLine(logText);
                    }
                }
            }
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
