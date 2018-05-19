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

namespace Seagal_TransformHttpContentToHttps.Analys
{
    public class SourceRewrites : ISourceRewrites
    {
        public string Name => "img-src";
        private static Regex ImageRegex = new Regex( @"<img>*", RegexOptions.Compiled );
        private static Regex UrlHttpRegex = new Regex($"src=[\"'](.+?)[\"'].+?", RegexOptions.Compiled );

        private List<HttpLink> imageAnalysList = new List<HttpLink>();
        private Serializer _serializer = new Serializer();

        public SourceRewrites( ILoggerFactory loggerFactory )
            : this( loggerFactory.CreateLogger<SourceRewrites>() )
        {
        }

        public SourceRewrites( ILogger logger ) => Logger = logger ?? throw new ArgumentNullException( nameof( logger ) );

        private ILogger Logger { get; }

        public void Execute( Context context )
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;

            var time = DateTime.Now.ToString( "yyyyMMddHHmmss" );
            var pathFail = @"C:\Users\evhop\Dokument\dumps\Https.txt".Replace( ".txt", $"_{time}_fail.txt" );
            var pathSuccess = @"C:\Users\evhop\Dokument\dumps\Https.txt".Replace( ".txt", $"_{time}_success.txt" );

            if( File.Exists( pathFail ) )
            {
                File.Delete( pathFail );
            }

            if( File.Exists( pathSuccess ) )
            {
                File.Delete( pathSuccess );
            }

            using( var client = clientFactory.CreateClient( settings.DestinationBuildConnectionString()) )
            {
                using( var connection = client.CreateConnection() )
                {
                    client.GetTableSchema(connection, settings.DestinationDb.Schema);
                    ExecuteTransaction( context, client, connection);
                }
            }
            //Skriv ut filen
            var distinctList = imageAnalysList.Distinct().ToList();
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

        private void ExecuteTransaction( IContext context, IWPClient client, IConnection connection)
        {
            using( var transaction = connection.BeginTransaction() )
            {
                try
                {
                    //Hämta länkar för Post
                    var posts = client.GetPosts(connection);
                    if( posts.Any() )
                    {
                        GetHttpForPost(posts);
                    }

                    //TODO Hämta länkar för Postmeta, commentmeta, comments, users, usermeta
                    //var metas = client.GetPostMeta(connection);
                    //if (metas.Any())
                    //{
                    //    GetHttpForPostmeta(metas, site);
                    //}

                    //Avsluta transactionen
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void GetHttpForPost( IEnumerable<Post> posts)
        {
            foreach( var post in posts )
            {
                GetLink(post.Id, post.SchemaTable, post.Content);
                GetLink(post.Id, post.SchemaTable, post.ContentFiltered);
                GetLink(post.Id, post.SchemaTable, post.Excerpt);
                //TODO ska den här göras
                //GetLink(post.Id, post.SchemaTable, post.Guid);
            }
        }

        private void GetLink(ulong id, string schemaTable, string content)
        {
            var matches = UrlHttpRegex.Matches(content);
            foreach (Match match in matches)
            {
                if (!match.Success)
                {
                    continue;
                }

                var src = match.Groups[1].Value;
                var srcHttps = src.Replace("http", "https");

                var httpLink = new HttpLink
                {
                    SchemaTable = schemaTable,
                    Id = id,
                    HttpSource = src
                };

                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(srcHttps);
                    request.Method = "HEAD";
                    request.Timeout = 2000;
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
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
                        request.Timeout = 2000;
                        var response = (HttpWebResponse)request.GetResponse();
                        //finns som http men inte som https
                        httpLink.Succeded = false;
                    }
                    catch (Exception ex)
                    {
                        //finns inte som http
                        httpLink.Succeded = null;
                    }
                }

                imageAnalysList.Add(httpLink);
            }
        }

        private void GetHttpForPostmeta( IEnumerable<Meta> postMetas, ISite site )
        {
            MetaUrlRewriter metaUrlRewriter = new MetaUrlRewriter(site);
            foreach ( var postMeta in postMetas )
            {
                var data = _serializer.Deserialize(postMeta.MetaValue);
                data = metaUrlRewriter.RewriteUrl(data);
                postMeta.MetaValue = _serializer.Serialize(data);
                //TODO lägga till i listan
            }
        }
    }
}
