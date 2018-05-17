using Migration.SQL.Interface;
using Migration.SQL.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Migration.Analys
{
    public class ImageSourceRewritesAnalys : IAnalys
    {
        public string Name => "img-src";
        private static Regex ImageRegex = new Regex( @"<img>*", RegexOptions.Compiled );
        private static Regex regex = new Regex( "<img\\s+[^>]*?src=\"([^\"\\r\\n<>]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase );

        private List<ImageAnalys> imageAnalysList = new List<ImageAnalys>();
        private PhpDeserializer deserializer = new PhpDeserializer();

        public ImageSourceRewritesAnalys( ILoggerFactory loggerFactory )
            : this( loggerFactory.CreateLogger<ImageSourceRewritesAnalys>() )
        {
        }

        public ImageSourceRewritesAnalys( ILogger logger ) => Logger = logger ?? throw new ArgumentNullException( nameof( logger ) );

        private ILogger Logger { get; }

        public void Execute( RunContext context )
        {
            var clientFactory = context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = context.Settings;
            var blobContainer = settings.Environment.AzureBlobContainer;

            if( blobContainer.EndsWith( "/" ) )
            {
                blobContainer = blobContainer.TrimEnd( '/' );
            }

            var time = DateTime.Now.ToString( "yyyyMMddHHmmss" );
            var pathFail = context.Options.Databasefile.Replace( ".txt", $"_{time}_fail.txt" );
            var pathSuccess = context.Options.Databasefile.Replace( ".txt", $"_{time}_success.txt" );

            if( File.Exists( pathFail ) )
            {
                File.Delete( pathFail );
            }

            if( File.Exists( pathSuccess ) )
            {
                File.Delete( pathSuccess );
            }

            foreach( var site in context.GetSites() )
            {
                using( var client = clientFactory.CreateClient( settings.Environment.DB.BuildConnectionString(), site.Generator ) )
                {
                    using( var connection = client.CreateConnection() )
                    {
                        client.GetTableSchema(connection, settings.Environment.DB.Database);
                        ExecuteTransaction( context, blobContainer, site, client, connection);
                    }
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
                        var logText = $"{x.SiteAzureContainer}\t{x.Id}\t{x.ImageSource}";
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

        private void ExecuteTransaction( IRunContext context, string blobContainer, ISite site, IWPClient client, IConnection connection)
        {
            var options = context.Options;

            using( var transaction = connection.BeginTransaction() )
            {
                try
                {
                    //Hämta bildlänkar för Post, Content
                    var contents = client.GetRegexPostContents( connection, ImageRegex.ToString() );
                    if( contents.Any() )
                    {
                        GetImageForPostContent(contents, blobContainer, site.AzureContainer);
                    }

                    //Hämta bildlänkar för Post, Guid 
                    var guids = client.GetPostGuids( "attachment" );
                    guids = guids.Where( guid => !string.IsNullOrEmpty( guid.Guid ) && !guid.Guid.Contains( "?attac" ) );
                    if( guids.Any() )
                    {
                        GetImageForPostGuid(guids, blobContainer, site.AzureContainer);
                    }

                    //Hämta bildlänkar för Postmeta, url och thumbnail
                    var metas = client.GetPostMeta( connection, "windows_azure_storage_info" );
                    if (metas.Any())
                    {
                        GetImageForPostmeta(metas, blobContainer, site);
                    }

                    //Avsluta transactionen
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void GetImageForPostContent( IEnumerable<PostContent> contents, string blobContainer, string siteAzureContainer )
        {
            foreach( var content in contents )
            {
                var matches = regex.Matches( content.Content );
                foreach( Match match in matches )
                {
                    if( !match.Success )
                    {
                        continue;
                    }

                    var src = match.Groups[1].Value;

                    var imageAnalys = new ImageAnalys
                    {
                        SiteAzureContainer = siteAzureContainer,
                        Id = content.Id,
                        TableSource = "",
                        ImageSource = src
                    };
                    if (imageAnalys.ImageSource.Contains(blobContainer))
                    {
                        imageAnalys.Succeded = true;
                    }
                    //data: ska inte komma med i någon fil
                    else if (imageAnalys.ImageSource.StartsWith("data:"))
                    {
                        imageAnalys.Succeded = null;
                    }
                    else
                    {
                        imageAnalys.Succeded = false;
                    }
                    imageAnalysList.Add(imageAnalys);
                }
            }
        }

        private void GetImageForPostGuid( IEnumerable<PostGuid> guids, string blobContainer, string siteAzureContainer )
        {
            foreach( var guid in guids )
            {
                var imageAnalys = new ImageAnalys
                {
                    SiteAzureContainer = siteAzureContainer,
                    Id = guid.Id,
                    TableSource = "",
                    ImageSource = guid.Guid
                };

                imageAnalys.Succeded = (imageAnalys.ImageSource.Contains( blobContainer )) ? true : false;
                imageAnalysList.Add( imageAnalys );
            }
        }

        private void GetImageForPostmeta( IEnumerable<PostMeta> postMetas, string blobContainer, ISite site )
        {
            foreach( var postMeta in postMetas )
            {
                var attachmentsMeta = postMeta.MetaValue;
                var imageAnalys = GetUrl( deserializer, postMeta.PostId, attachmentsMeta, site.AzureContainer );
                if( imageAnalys != null )
                {
                    imageAnalysList.Add( imageAnalys );
                }

                imageAnalys = GetThumbnail( deserializer, postMeta.PostId, attachmentsMeta, blobContainer,site );
                if( imageAnalys != null )
                {
                    imageAnalysList.Add( imageAnalys );
                }
            }
        }

        private ImageAnalys GetUrl( PhpDeserializer deserializer, ulong postId, string attachmentsMeta, string siteAzureContainer)
        {
            var attachments = deserializer.DeserializeFromString( attachmentsMeta );
            var url = attachments.GetValue<string>( "url" );

            var imageAnalys = new ImageAnalys
            {
                SiteAzureContainer = siteAzureContainer,
                Id = postId,
                TableSource = "",
                ImageSource = url,
                Succeded = true
            };

            return imageAnalys;
        }

        private ImageAnalys GetThumbnail( PhpDeserializer deserializer, ulong postId, string attachmentsMeta, string blobContainer, ISite site)
        {
            var attachments = deserializer.DeserializeFromString( attachmentsMeta );
            var container = attachments.GetValue<string>( "container" );

            var thumbnail = attachments.GetValue<IDictionary<object, object>>( "thumbnails" );

            if( thumbnail.Count == 0 )
            {
                return null;
            }
            var thumbnailFile = thumbnail.First().Value;
            var imageAnalys = new ImageAnalys
            {
                SiteAzureContainer = site.AzureContainer,
                Id = postId,
                TableSource = "",
                ImageSource = site.Settings.Multisite ? $"{blobContainer}/{FormatBlogId(site.Index)}/{thumbnailFile}" : $"{blobContainer}/{container}/{thumbnailFile}",
                Succeded = true
            };
            return imageAnalys;
        }

        private static string FormatBlogId(long id, bool addZeros = true)
        {
            if (addZeros)
            {
                return $"{id:D3}";
            }
            else
            {
                return id.ToString();
            }
        }

    }
}
