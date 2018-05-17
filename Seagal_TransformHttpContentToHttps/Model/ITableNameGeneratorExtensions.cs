using Seagal_TransformHttpContentToHttps.WPClient;
using Seagal_TransformHttpContentToHttps.WPClient.Model;
using System;

namespace Seagal_TransformHttpContentToHttps.Model
{
    public static class ITableNameGeneratorExtensions
    {
        public static ISite CreateSite(this ITableNameGenerator generator, IContext runContext)
        {
            /*
            if (generator is IndexedTableNameGenerator indexed)
            {
                return new Core.Blog(indexed.Index, indexed.Path, generator, runContext);
            }
            else if (generator is StandardTableNameGenerator standard)
            {
                return new Core.MultiSite(generator, runContext);
            }
            */
            throw new ArgumentException("Unknown generator", nameof(generator));
        }
    }
}
