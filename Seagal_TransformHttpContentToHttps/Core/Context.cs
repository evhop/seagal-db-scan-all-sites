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
    }
}
