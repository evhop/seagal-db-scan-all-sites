using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using WPDatabaseWork.WPClient.View;
using WPDatabaseWork.WPClient.Model;
using Microsoft.Extensions.DependencyInjection;
using WPDatabaseWork.Model;

namespace WPDatabaseWork.Core
{
    public class HtmlParser
    {
        private static Regex _tagRegex = new Regex($"<img[^>]+>", RegexOptions.Compiled);
        private static Regex _idRegex = new Regex($"wp-image-([0-9]+)", RegexOptions.Compiled);
        private static Regex _altRegex = new Regex($"alt=\"([^\"]+)\"", RegexOptions.Compiled);
        private IContext _context;
        private List<Post> _attachments = new List<Post>();

        public HtmlParser (IContext context)
        {
            _context = context;
            _attachments = GetAttachments();
        }

        public string ConvertToHtml(string content, ulong postId)
        {
            try
            {
                var elements = _tagRegex.Matches(content).ToList();

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
                            content = content.Replace(element.ToString(), newElement);
                        }
                        else
                        {
                            content = "not exists";
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error ConvertToHtml " + e);
            }

            return content;
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
                    client.GetTableSchema(connection, settings.DestinationDb.Schema);
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
    }
}
