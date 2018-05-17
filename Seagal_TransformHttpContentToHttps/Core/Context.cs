using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Seagal_TransformHttpContentToHttps.WPClient.Model;
using Seagal_TransformHttpContentToHttps.WPClient;
using Seagal_TransformHttpContentToHttps.Model;
using Seagal_TransformHttpContentToHttps.View;

namespace Seagal_TransformHttpContentToHttps.Core
{
    public class Context : IContext
    {
        public IServiceProvider ServiceProvider { get; }
        public Settings Settings { get; }

        public Context(Settings settings, IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IEnumerable<ISite> GetSites() => GetGenerators().Select(generator => generator.CreateSite(this));

        public IEnumerable<ITableNameGenerator> GetGenerators()
        {
            var nameGenerators = new List<ITableNameGenerator>();

            var clientFactory = ServiceProvider.GetService<IWPClientFactory>();

            using (var client = clientFactory.CreateClient(Settings.DestinationBuildConnectionString(), new StandardTableNameGenerator(Settings.TablePrefix)))
            {
                using (var connection = client.CreateConnection())
                {
                    //var blog = client.GetBlog(connection, JobSetting.GetDomain, JobSetting.GetBlogName);
                    //nameGenerators.Add(new IndexedTableNameGenerator(Settings.TablePrefix, blog.BlogId, blog.Path));
                }
            }
            return nameGenerators;
        }
    }
}
