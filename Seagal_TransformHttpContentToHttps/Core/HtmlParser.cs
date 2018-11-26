using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using WPDatabaseWork.WPClient.View;
using WPDatabaseWork.WPClient.Model;
using Microsoft.Extensions.DependencyInjection;
using WPDatabaseWork.Model;
using WPDatabaseWork.Analys;

namespace WPDatabaseWork.Core
{
    public class HtmlParser
    {
        private static Regex _imgRegex = new Regex($"<img[^>]+>", RegexOptions.Compiled);
        private static Regex _aRegex = new Regex($"<\\s*a[^>]*>(.*?)<\\s*\\/\\s*a>", RegexOptions.Compiled);
        private static Regex _hrefRegex = new Regex($"href=\"([^\"]+)\"", RegexOptions.Compiled);
        private static Regex _idRegex = new Regex($"wp-image-([0-9]+)", RegexOptions.Compiled);
        private static Regex _srcRegex = new Regex($"src=\"([^\"]+)\"", RegexOptions.Compiled);
        private static Regex _altRegex = new Regex($"alt=\"([^\"]+)\"", RegexOptions.Compiled);
        private IContext _context;
        private List<Post> _attachments = new List<Post>();
        private List<Meta> _attachmentMetas = new List<Meta>();

        public HtmlParser (IContext context)
        {
            _context = context;
            _attachments = GetAttachments();
            _attachmentMetas = GetAttachedFiles();
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
            return name.ToLower().Replace("+", "").Replace(' ', '_').Replace('å', 'a').Replace('ä', 'a').Replace('ö', 'o').Replace("?", "").Replace("!", "");
        }

    }
}
