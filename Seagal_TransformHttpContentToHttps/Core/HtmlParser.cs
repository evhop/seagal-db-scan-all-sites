using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using WPDatabaseWork.WPClient.View;
using WPDatabaseWork.WPClient.Model;
using Microsoft.Extensions.DependencyInjection;
using WPDatabaseWork.Model;
using WPDatabaseWork.Analys;
using System.IO;
using WPDatabaseWork.View;
using System.Drawing;

namespace WPDatabaseWork.Core
{
    public class HtmlParser
    {
        private static Regex _imgRegex = new Regex($"<img[^>]+>", RegexOptions.Compiled);
        private static Regex _aRegex = new Regex($"<\\s*a[^>]*>(.*?)<\\s*\\/\\s*a>", RegexOptions.Compiled);
        private static Regex _hrefRegex = new Regex($"href=\"([^\"]+)\"", RegexOptions.Compiled);
        private static Regex _srcRegex = new Regex($"src=\"([^\"]+)\"", RegexOptions.Compiled);
        private static Regex _idRegex = new Regex($"wp-image-([0-9]+)", RegexOptions.Compiled);
        private static Regex _altRegex = new Regex($"alt=\"([^\"]+)\"", RegexOptions.Compiled);
        private static Regex _widthRegex = new Regex($"width=\"([0-9]+)\"", RegexOptions.Compiled);
        private static Regex _heightRegex = new Regex($"height=\"([0-9]+)\"", RegexOptions.Compiled);
        private static Regex _sizeRegex = new Regex($"-([0-9]+)x([0-9]+)", RegexOptions.Compiled);
        private IContext _context;
        private List<Post> _attachments = new List<Post>();
        private List<Meta> _attachmentMetas = new List<Meta>();

        public List<Post> _updateParentIds { get; set; }

        public HtmlParser (IContext context, bool getAttachment = true)
        {
            _context = context;
            if (getAttachment)
            {
                _attachments = GetAttachments();
                _attachmentMetas = GetAttachedFiles();
            }
        }

        public List<Post> AltTagTV(Post post)
        {
            List<Post> replaceContents = new List<Post>();

            try
            {
                var elements = _imgRegex.Matches(post.Content).ToList();

                foreach (var element in elements)
                {
                    if (element.Value.Contains("tveassets"))
                    {
                        var idMatch = _idRegex.Match(element.ToString());
                        if (idMatch.Captures.Count > 0)
                        {
                            ulong id = Convert.ToUInt32(idMatch.Groups[1].Value);

                            var altMatch = _altRegex.Match(element.ToString());
                            string alt = altMatch.Groups[1].Value;

                            var title = _attachments.Where(a => a.Id == id).Select(e => e.OldContent).FirstOrDefault();

                            //Ta bort tabbar och _
                            alt = title.Replace("_", " ").Replace("\t", " ");
                            while (alt.IndexOf("  ") > 0)
                            {
                                alt = alt.Replace("  ", " ");
                            }

                            string newElement = element.ToString().Replace("alt=\"\"", "alt=\"" + alt + "\"");
                            replaceContents.Add(new Post
                            {
                                SchemaTable = post.SchemaTable,
                                Id = post.Id,
                                OldContent = element.ToString(),
                                Content = newElement
                            });
                        }
                        else
                        {
                            replaceContents.Add(new Post
                            {
                                Id = post.Id,
                                Content = "not exists"
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error ConvertToHtml " + e);
            }

            return replaceContents;
        }

        public List<Post> replaceUrl(List<HttpLink> links, Post post, bool onlyHref)
        {
            List<Post> replacedElements = new List<Post>();
            try
            {
                //kollar först om det finns a element
                var elements = _aRegex.Matches(post.Content).ToList();
                foreach (var element in elements)
                {
                    foreach (var link in links)
                    {
                        if (element.Value.Contains(link.HttpSource))
                        {
                            var idMatch = _idRegex.Match(element.ToString());
                            if (idMatch.Captures.Count > 0)
                            {
                                ulong id = Convert.ToUInt32(idMatch.Groups[1].Value);
                                var meta = _attachmentMetas.Where(a => a.PostId == id).FirstOrDefault();
                                string url = "";
                                if (post.SchemaTable.Contains("`blogg_veckorevyn_com`.wp_11383_posts"))
                                {
                                    url = "https://vrbassetsprod.blob.core.windows.net/11383/" + meta.MetaValue;
                                }

                                var srcMatch = _srcRegex.Match(element.ToString());
                                var hrefMatch = _hrefRegex.Match(element.ToString());

                                //fixar scr recplace
                                if (!onlyHref)
                                {
                                    replacedElements.Add(ReplacementPost(post, srcMatch.Groups[1].Value, url, "src"));
                                }

                                //fixar href replace
                                replacedElements.Add(ReplacementPost(post, hrefMatch.Groups[1].Value, url, "href"));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error ConvertToHtml " + e);
            }

            return replacedElements;
        }

        private Post ReplacementPost(Post post, string oldUrl, string url, string type)
        {
            return new Post
            {
                Id = post.Id,
                Date = post.Date,
                SchemaTable = post.SchemaTable,
                OldContent = $"{type}=\"{oldUrl}\"",
                Content = $"{type}=\"{url}\""
            };
        }

        public List<Post> ImageVanja(Post post)
        {
            List<Post> replaceContents = new List<Post>();
            try
            {
                //kollar först om det finns a element
                var elements = _aRegex.Matches(post.Content).ToList();
                foreach (var element in elements)
                {
                    if (element.Value.Contains("vanja.metromode"))
                    {
                        var srcMatch = _srcRegex.Match(element.ToString());
                        var hrefMatch = _hrefRegex.Match(element.ToString());

                        string oldSrc = srcMatch.Groups[1].Value;
                        string newSrc = srcMatch.Groups[1].Value.Replace("http://vanja.metromode.se/files/", "https://mambassetsprod.blob.core.windows.net/11609/");

                        string oldHref = hrefMatch.Groups[1].Value;
                        string newHref = hrefMatch.Groups[1].Value.Replace("http://vanjaw.com/wp-content/uploads/", "https://mambassetsprod.blob.core.windows.net/11609/");

                        replaceContents.Add(ReplacementPost(post, srcMatch.Groups[1].Value, newSrc, "src"));
                        replaceContents.Add(ReplacementPost(post, hrefMatch.Groups[1].Value, newHref, "href"));
                    }
                }

                elements = _imgRegex.Matches(post.Content).ToList();
                foreach (var element in elements)
                {
                    if (element.Value.Contains("vanja.metromode"))
                    {
                        var srcMatch = _srcRegex.Match(element.ToString());
                        if (srcMatch.Captures.Count > 0)
                        {
                            string oldSrc = srcMatch.Groups[1].Value;
                            string newSrc = srcMatch.Groups[1].Value.Replace("http://vanja.metromode.se/files/", "https://mambassetsprod.blob.core.windows.net/11609/").Replace("http://vanjaw.com/wp-content/uploads/"	, "https://mambassetsprod.blob.core.windows.net/11609/");

                            var newPost = ReplacementPost(post, srcMatch.Groups[1].Value, newSrc, "src");
                            if (replaceContents.FirstOrDefault(x => x.Id == newPost.Id && x.OldContent == newPost.OldContent) == null)
                            {
                                replaceContents.Add(newPost);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error ConvertToHtml " + e);
            }

            return replaceContents;
        }

        public List<Attachment> ImagesTailsweep(Post post)
        {
            List<Attachment> replaceContents = new List<Attachment>();
             _updateParentIds = new List<Post>();
            try
            {
                //kollar först om det finns a element
                var elements = _imgRegex.Matches(post.Content).ToList();
                foreach (var element in elements)
                {
                    if (element.Value.Contains("tsbassetsprod"))
                    {
                        var srcMatch = _srcRegex.Match(element.ToString());
                        if (srcMatch.Captures.Count > 0)
                        {
                            string oldSrc = srcMatch.Groups[1].Value;
                            if (_attachments.Exists(x => x.Content == oldSrc))
                            {
                                var updatePost = _attachments.Find(x => x.Content == oldSrc);
                                //Ändra bara om det inte finns en parent
                                if (updatePost.ParentId == 0)
                                {
                                    updatePost.ParentId = post.Id;
                                    _updateParentIds.Add(updatePost);
                                }
                                else
                                {
                                    //Kolla om jag kommer hit
                                }
                            }
                            else
                            {
                                string title = GetValue(element, _altRegex);
                                string name = Sanitice(title);

                                var blobFileName = oldSrc.Substring(oldSrc.LastIndexOf('/') + 1);
                                var blobFilePath = oldSrc.Replace("https://tsbassetsprod.blob.core.windows.net/010/", "");

                                if (String.IsNullOrEmpty(title))
                                {
                                    title = blobFileName.Remove(blobFileName.LastIndexOf('.')).Replace('_', ' ');
                                    name = Sanitice(title);
                                }
                                var newPost = new Attachment
                                {
                                    Author = 1,
                                    Content = "",
                                    Date = DateTime.Parse(post.Date),
                                    DateGMT = DateTime.Parse(post.Date),
                                    Title = title,
                                    Name = name,
                                    Guid = oldSrc,
                                    ParentId = post.Id,
                                    MimeType = GetMimeType(oldSrc)
                                };

                                //Skapar postmeta
                                newPost.Metas = GetImageMetaData(newPost, blobFileName, blobFilePath);

                                //Lägg bara till posten om det går att läsa filen
                                if (newPost.Metas != null)
                                {
                                    replaceContents.Add(newPost);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error ConvertToHtml " + e);
            }

            return replaceContents;
        }

        private static string GetValue(Match element, Regex regex)
        {
            string value = "";
            var match = regex.Match(element.ToString());
            if (match.Captures.Count > 0)
            {
                value = match.Groups[1].Value;
            }

            return value;
        }

        private List<Post> GetAttachments()
        {
            var clientFactory = _context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = _context.Settings;
            List<Post> posts = new List<Post>();

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
                            return posts = client.GetAttachments(connection).ToList();
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
        private List<Meta> GetAttachedFiles()
        {
            var clientFactory = _context.ServiceProvider.GetService<IWPClientFactory>();
            var settings = _context.Settings;
            List<Meta> metas = new List<Meta>();

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
                            return metas = client.GetPostMeta(connection, "_wp_attached_file").ToList();
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

        private string Sanitice(string name)
        {
            return name.ToLower().Replace("+", "_").Replace(' ', '_').Replace('å', 'a').Replace('ä', 'a').Replace('ö', 'o').Replace("?", "").Replace("!", "").Replace(".", "");
        }

        #region HelpImages
        private List<Meta> GetImageMetaData(Attachment attachment, string blobFileName, string blobFilePath)
        {
            List<Meta> postMetas = new List<Meta>();

            //WindowStorage
            List<string> blobThumbnails = new List<string>();
            PostMetaWindowStorage windowStorage = new PostMetaWindowStorage
            {
                blob = blobFilePath,
                url = attachment.Guid,
                container = "010",
                thumbnails = blobThumbnails
            };
            Meta postMeta = new Meta
            {
                SchemaTable = "blogg_tailsweep_se.wp_10_postmeta",
                MetaKey = "windows_azure_storage_info",
                MetaValue = SerializerHelpers.SerializeToString(windowStorage)
            };
            postMetas.Add(postMeta);

            postMeta = new Meta
            {
                //wp_attached_file
                SchemaTable = "blogg_tailsweep_se.wp_10_postmeta",
                MetaKey = "_wp_attached_file",
                MetaValue = blobFilePath
            };
            postMetas.Add(postMeta);

            //_wp_attachment_metadata
            PostAttachmentImageMeta postAttachmentImageMeta = new PostAttachmentImageMeta
            {
                aperture = "",
                title = "",
                camera = "",
                caption = "",
                copyright = "",
                credit = "",
                created_timestamp = "",
                focal_length = "",
                iso = "",
                shutter_speed = "",
                orientation = "",
                keywords = new List<string>()
            };

            List<PostAttachmentSize> postAttachmentSizes = new List<PostAttachmentSize>();

            PostAttachment postAttachment = new PostAttachment
            {
                file = blobFilePath,
                sizes = postAttachmentSizes,
                image_meta = postAttachmentImageMeta,
            };

            try
            {
                var inputPath = @"C:\Users\evhop\Documents\TestImportBlogg\monasuniversum\" + blobFilePath;
                using (var image = new Bitmap(Image.FromFile(inputPath)))
                {
                    postAttachment.width = image.Width;
                    postAttachment.height = image.Height;
                }
            }
            catch (OutOfMemoryException e)
            {
                Console.WriteLine("Error OutOfMemory " + e);
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error GetImageSizes " + e);
                return null;
            }

            postMeta = new Meta
            {
                //wp_attached_file
                SchemaTable = "blogg_tailsweep_se.wp_10_postmeta",
                MetaKey = "_wp_attachment_metadata",
                MetaValue = SerializerHelpers.SerializeToString(postAttachment).Replace("image_meta", "image-meta")
            };
            postMetas.Add(postMeta);

            return postMetas;
        }

        private string GetMimeType(string guid)
        {
            string defaultMimeType = "image/jpeg";
            string ext = "";
            try
            {
                ext = Path.GetExtension(guid).TrimStart('.');
            }
            catch (Exception e)
            {
                return defaultMimeType;
            }

            string mimeType = ImageExtension(ext) ? "image/" + ext : AudioExtension(ext) ? "audio/" + ext : VideoExtension(ext) ? "video/" + ext : DocumentExtension(ext) ? "application/" + ext : defaultMimeType;
            return mimeType;
        }

        private bool ImageExtension(string ext)
        {
            string[] images = { "png", "ico", "tiff", "tif", "psd", "gif", "bmp" };
            return images.Contains(ext);
        }

        private bool AudioExtension(string ext)
        {
            string[] audios = { "mp3", "wav" };
            return audios.Contains(ext);
        }

        private bool VideoExtension(string ext)
        {
            string[] videos = { "mov", "mp4", "3gp", "3gpp", "swf" };
            return videos.Contains(ext);
        }

        private bool DocumentExtension(string ext)
        {
            string[] docs = { "doc", "docx", "pdf", "xlsx", "txt", "html", "zip" };
            return docs.Contains(ext);
        }
        #endregion
    }
}
