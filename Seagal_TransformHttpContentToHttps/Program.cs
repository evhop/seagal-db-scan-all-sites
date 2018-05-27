using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StructureMap;
using Fallback_blogg.Core;
using Fallback_blogg.View;
using Fallback_blogg.WPClient.Model;
using Fallback_blogg.WPClient;
using Fallback_blogg.Analys;
using System.Collections.Generic;

namespace Fallback_blogg
{
    public class Program
    {
        private static class ServiceLocator
        {
            public static void Initialize(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;

            public static IServiceProvider ServiceProvider { get; private set; }
        }

        public static Context Context { get; set; }

        static void Main(string[] args)
        {
            SetupDependencyInjection();
            AnalysRepository.Initialize(ServiceLocator.ServiceProvider);

            ExecuteCommand();
            return;
        }

        private static void ExecuteCommand()
        {
            SetSettings(BuildSettings(), ServiceLocator.ServiceProvider);
            Execute();
        }

        private static void SetSettings(Settings settings, IServiceProvider serviceProvider)
        {
            Context = new Context(settings, serviceProvider);
            //_pageProcessor = new PostProcessor(Context);
        }

        private static Settings BuildSettings()
        {
            var builder = new ConfigurationBuilder();
            var fileInfo = new FileInfo("configuration/appsettings.job");
            builder.AddJsonFile(fileInfo.FullName, false, false);

            var settings = new Settings();
            var configuration = builder.Build();
            configuration.Bind(settings);

            return settings;
        }

        private static void SetupDependencyInjection()
        {
            var services = new ServiceCollection()
                    .AddLogging();

            var container = new Container();
            container.Configure(config =>
            {
                config.Scan(_ =>
                {
                    _.AssemblyContainingType(typeof(Program));
                    _.AssemblyContainingType(typeof(WPClient.WPClient));
                    _.WithDefaultConventions();
                });

                config.Populate(services);
            });

            var serviceProvider = container.GetInstance<IServiceProvider>();

            ServiceLocator.Initialize(serviceProvider);
        }
        public static void Execute()
        {
            var analysRepository = ServiceLocator.ServiceProvider.GetService<IAnalysRepository>();
            var analys = "img-src";
            var instance = analysRepository.GetAnalys(analys);

            try
            {
                //Kör för varje databas
                foreach (var db in Context.Settings.Db)
                {
                    Context.Settings.DestinationDb = db;
                    IEnumerable<string> schemas = GetSchema();

                    foreach (var schema in schemas)
                    {
                        if (!schema.Contains("blogg"))
                        {
                            continue;
                        }

                        Context.Settings.DestinationDb.Schema = schema;
                        instance.Execute(Context);
                    }
                }

                Console.WriteLine("done - the blogg updated");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static IEnumerable<string> GetSchema()
        {
            var clientFactory = Context.ServiceProvider.GetService<IWPClientFactory>();

            using (var client = clientFactory.CreateClient(Context.Settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    return client.GetSchema(connection);
                }
            }
        }
    }
}
