using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StructureMap;
using WPDatabaseWork.Core;
using WPDatabaseWork.View;
using WPDatabaseWork.WPClient.Model;
using WPDatabaseWork.WPClient;
using WPDatabaseWork.Analys;
using System.Collections.Generic;
using System.Linq;

namespace WPDatabaseWork
{
    public class Program
    {
        #region properties
        private static class ServiceLocator
        {
            public static void Initialize(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;

            public static IServiceProvider ServiceProvider { get; private set; }
        }

        public static Context Context { get; set; }

        public static Dictionary<string, string> GetRunSwitchMappings() => new Dictionary<string, string>
        {
            { "-p", "preview" },
            { "-f", "function" },
            { "-b", "brand" }
        };
        #endregion

        static void Main(string[] args)
        {
            SetupDependencyInjection();
            AnalysRepository.Initialize(ServiceLocator.ServiceProvider);
            SetSettings(BuildOptions(args.Skip(1).ToArray()), BuildSettings(), ServiceLocator.ServiceProvider);

            var command = args[0];
            switch (command)
            {
                case "blogg":
                    ExecuteBloggCommand();
                    break;

                case "caro":
                    ExecuteCarolinesCommand();
                    break;

                case "http":
                    ExecuteHttpCommand();
                    break;
                case "aom":
                    ExecuteReceptCommand();
                    break;
                case "alttag":
                    ExecuteAltTagCommand();
                    break;
            }
            return;
        }

        #region ExecuteCommand
        private static void ExecuteHttpCommand()
        {
            var analysRepository = ServiceLocator.ServiceProvider.GetService<IAnalysRepository>();
            var analys = "http";
            var instance = analysRepository.GetAnalys(analys);

            var time = Context.Options.Brand + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            try
            {
                //Kör för varje databas
                foreach (var db in Context.Settings.Db)
                {
                    Context.Settings.DestinationDb = db;
                    IEnumerable<string> schemas = GetSchema();

                    foreach (var schema in schemas)
                    {
                        
                        //Börjar med att hoppa över blogg
                        if (!schema.Contains(Context.Options.Brand))
                        {
                            continue;
                        }
                        
                        Context.Settings.DestinationDb.Schema = schema;
                        ExecuteHttp(instance, time);
                    }
                }

                //Skriva ut allt till fil
                instance.WriteUrlToFile($@"C:\Users\evhop\Dokument\dumps\Http_{time}_{Context.Options.Function}");
              Console.WriteLine("done - scaned all databases");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void ExecuteAltTagCommand()
        {
            var analysRepository = ServiceLocator.ServiceProvider.GetService<IAnalysRepository>();
            var analys = "alttag";
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

                        //Ska endast göras för TV
                        if (!schema.Contains("teknikensvarld_se"))
                        {
                            continue;
                        }

                        Context.Settings.DestinationDb.Schema = schema;
                        ExecuteAltTag(instance);
                    }
                }

                //Skriva ut allt till fil
                Console.WriteLine("done - changed the recipe links");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void ExecuteAltTag(ISourceRewrites instance)
        {
            instance.Execute(Context, DateTime.Now.ToShortDateString());
        }

        private static void ExecuteReceptCommand()
        {
            var analysRepository = ServiceLocator.ServiceProvider.GetService<IAnalysRepository>();
            var analys = "aom";
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

                        //Börjar med att hoppa över blogg
                        if (!schema.Contains("alltommat_se"))
                        {
                            continue;
                        }

                        Context.Settings.DestinationDb.Schema = schema;
                        ExecuteRecept(instance);
                    }
                }

                //Skriva ut allt till fil
                Console.WriteLine("done - changed the recipe links");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void ExecuteRecept(ISourceRewrites instance)
        {
            instance.Execute(Context, DateTime.Now.ToShortDateString());
        }

        private static void ExecuteHttp(ISourceRewrites instance, string time)
        {
            switch(Context.Options.Function)
            {
                case "getdomain":
                    instance.Execute(Context, time);
                    break;
                case "updomain":
                    instance.ExecuteUpdate(Context);
                    break;
                case "all":
                    instance.Execute(Context, time);
                    break;

                default:
                    break;
            }
        }

        private static void ExecuteBloggCommand()
        {
            var analysRepository = ServiceLocator.ServiceProvider.GetService<IAnalysRepository>();
            var analys = "img-src";
            var instance = analysRepository.GetAnalys(analys);

            try
            {
                //Kör för varje databas
                foreach (var db in Context.Settings.Db)
                {
                    var time = $"_{db.Host}_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                    Context.Settings.DestinationDb = db;
                    IEnumerable<string> schemas = GetSchema();

                    foreach (var schema in schemas)
                    {
                        if (!schema.Contains("blogg"))
                        {
                            continue;
                        }

                        Context.Settings.DestinationDb.Schema = schema;
                        instance.Execute(Context, time);
                    }
                }

                Console.WriteLine("done - the blogg updated");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        private static void ExecuteCarolinesCommand()
        {
            var analysRepository = ServiceLocator.ServiceProvider.GetService<IAnalysRepository>();
            var analys = "img-car";
            var instance = analysRepository.GetAnalys(analys);

            try
            {
                //Kör för varje databas
                foreach (var db in Context.Settings.Db)
                {
                    var time = $"_{db.Host}_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                    Context.Settings.DestinationDb = db;
                    IEnumerable<string> schemas = GetSchema();

                    foreach (var schema in schemas)
                    {
                        if (!schema.Contains("blogg_damernasvarld"))
                        {
                            continue;
                        }

                        Context.Settings.DestinationDb.Schema = schema;
                        instance.Execute(Context, time);
                    }
                }

                Console.WriteLine("done - the blogg updated");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        #endregion


        private static Options BuildOptions(string[] args)
        {
            var dict = new Dictionary<string, string>
            {
                { "preview", "false" },
                { "function", null },
                { "brand", null }
            };

            var builder = new ConfigurationBuilder();
            builder
                .AddCommandLine(args, GetRunSwitchMappings());

            var options = new Options();

            var configuration = builder.Build();
            configuration.Bind(options);

            return options;
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

        private static void SetSettings(Options options, Settings settings, IServiceProvider serviceProvider)
        {
            Context = new Context(options, settings, serviceProvider);
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
