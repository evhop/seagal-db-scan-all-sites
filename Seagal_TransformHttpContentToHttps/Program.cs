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
            { "-b", "brand" },
            { "-i", "bloggid" },
            { "-y", "year" },
            { "-u", "updateurlfromimageid" }
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
                case "src":
                    ExecuteCommand(command, "blogg");
                    break;
                case "caro":
                    ExecuteCommand(command, "blogg_damernasvarld");
                    break;
                case "aom":
                    ExecuteCommand(command, "alltommat_se");
                    break;
                case "alttag":
                    ExecuteCommand(command, "teknikensvarld_se");
                    break;
                case "vanja":
                    ExecuteCommand(command, "blogg_mama");
                    break;
                case "restoreMedia":
                    ExecuteCommand(command, "blogg_tailsweep");
                    break;
                //href404 -b="alltommat_se" -y=""
                case "src404":
                case "href404":
                case "srcBMB":
                case "http":
                    ExecuteHttpCommand(command);
                    break;
            }
            return;
        }

        #region ExecuteCommand

        private static void ExecuteHttpCommand(string command)
        {
            try
            {
                //Kör för varje databas
                foreach (var db in Context.Settings.Db)
                {
                    var analysRepository = ServiceLocator.ServiceProvider.GetService<IAnalysRepository>();
                    var instance = analysRepository.GetAnalys(command);

                    Console.WriteLine($"start - {command} for {Context.Options.Brand}.{Context.Options.BloggId} and year {Context.Options.Year}");
                    var time = Context.Options.Brand + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");

                    Context.Settings.DestinationDb = db;
                    IEnumerable<string> schemas = GetSchema();

                    foreach (var schema in schemas)
                    {
                        //Börjar med och kolla om bran är satt annars kolla att den slutar på
                        if (!String.IsNullOrEmpty(Context.Options.Brand))
                        {
                            if (schema != Context.Options.Brand)
                            {
                                continue;
                            }
                        }

                        Console.WriteLine($"start - {schema}");
                        Context.Settings.DestinationDb.Schema = schema;
                        ExecuteHttp(instance, time);
                    }
                    string function = Context.Options.Function == "" ? "" : $"_{Context.Options.Function}";
                    string bloggId = Context.Options.BloggId == "" ? "" : $"_{Context.Options.BloggId}";
                    string year = Context.Options.Year == "" ? "" : $"_{Context.Options.Year}";

                    //Skriva ut allt till fil
                    instance.WriteUrlToFile($@"C:\Users\evhop\Dokument\dumps\{command}_{time}{function}{bloggId}{year}");
                    Console.WriteLine($"done - {command} for {Context.Options.Brand}.{Context.Options.BloggId} and year {Context.Options.Year}");
                }
                Console.WriteLine($"done - {command}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void ExecuteHttp(ISourceRewrites instance, string time)
        {
            switch (Context.Options.Function)
            {
                case "updomain":
                    instance.ExecuteUpdate(Context);
                    break;
                default:
                    instance.Execute(Context, time);
                    break;
            }
        }

        private static void ExecuteCommand(string command, string databaseName)
        {
            var analysRepository = ServiceLocator.ServiceProvider.GetService<IAnalysRepository>();
            var instance = analysRepository.GetAnalys(command);

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
                        //Börjar med och kolla om bran är satt annars kolla att den slutar på
                        if (!String.IsNullOrEmpty(databaseName))
                        {
                            if (!schema.StartsWith(databaseName))
                            {
                                continue;
                            }
                        }

                        Context.Settings.DestinationDb.Schema = schema;
                        //Context.Settings.DestinationSite.DestinationAzureBlob = GetAzureBlob();
                        //Context.Settings.DestinationSite.DestinationAzureBlobKey = GetAzureBlobKey();
                        instance.Execute(Context, time);
                    }
                }

                Console.WriteLine($"done - command: {command} for {databaseName}");
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
                { "brand", null },
                { "bloggid", null },
                { "year", null },
                { "updateurlfromimageid", "false" }
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
        private static string GetAzureBlobKey()
        {
            var clientFactory = Context.ServiceProvider.GetService<IWPClientFactory>();

            WPClient.View.Options azureBlobKey = new WPClient.View.Options();
            using (var client = clientFactory.CreateClient(Context.Settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, Context.Settings.DestinationDb.Schema);
                    azureBlobKey = client.GetOptionSetting(connection, "azure_storage_account_primary_access_key");
                }
            }
            return azureBlobKey.OptionValue;
        }

        private static string GetAzureBlob()
        {
            var clientFactory = Context.ServiceProvider.GetService<IWPClientFactory>();

            WPClient.View.Options azureBlob = new WPClient.View.Options();
            using (var client = clientFactory.CreateClient(Context.Settings.DestinationBuildConnectionString()))
            {
                using (var connection = client.CreateConnection())
                {
                    client.GetTableSchema(connection, Context.Settings.DestinationDb.Schema);
                    azureBlob = client.GetOptionSetting(connection, "azure_storage_account_name");
                }
            }
            return azureBlob.OptionValue;
        }
    }
}
