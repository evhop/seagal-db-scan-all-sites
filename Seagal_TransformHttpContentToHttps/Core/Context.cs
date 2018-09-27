using System;
using WPDatabaseWork.Model;
using WPDatabaseWork.View;

namespace WPDatabaseWork.Core
{
    public class Context : IContext
    {
        public IServiceProvider ServiceProvider { get; }
        public Settings Settings { get; }
        public Options Options { get; }

        public Context(Options options, Settings settings, IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }
    }
}
