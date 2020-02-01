using CommandLine;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Handlers;
using ELO.Models;
using ELO.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RavenBOT.Common;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ELO
{
    public class Program
    {
        public class Options
        {
            [Option('p', "path", Required = false, HelpText = "Path to a LocalConfig.json file")]
            public string Path { get; set; }
        }

        public IServiceProvider Provider { get; set; }
        public static void Main(string[] args)
        {
            var program = new Program();
            program.RunAsync(args).GetAwaiter().GetResult();
        }

        public virtual async Task RunAsync(string[] args = null)
        {
            //Parse cl arguments and set the variables
            if (args != null)
            {
                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(o =>
                    {
                        if (o.Path != null)
                        {
                            ConfigManager.ConfigPath = o.Path;
                        }
                    });
            }

            //Initialize new default required configs
            var localManagement = new ConfigManager();
            localManagement.GetConfig();
            var socketConfig = localManagement.LastConfig.GetConfig<DscSerializable>("SocketConfig")?.ToConfig();
            if (socketConfig == null)
            {
                localManagement.LastConfig.AdditionalConfigs.Add("SocketConfig", new DscSerializable());
                localManagement.SaveConfig(localManagement.LastConfig);
            }

            var commandConfig = localManagement.LastConfig.GetConfig<CscSerializable>("CommandConfig")?.ToConfig();
            if (commandConfig == null)
            {
                localManagement.LastConfig.AdditionalConfigs.Add("CommandConfig", new CscSerializable());
                localManagement.SaveConfig(localManagement.LastConfig);
            }

            var dbConfig = localManagement.LastConfig.GetConfig<DatabaseConfig>("DatabaseConfig");
            if (dbConfig == null)
            {
                var config = new DatabaseConfig();
                Console.WriteLine("Input SQL Server name");
                config.Server = Console.ReadLine();
                Console.WriteLine("Input SQL Database name");
                config.DatabaseName = Console.ReadLine();
                Console.WriteLine("Input SQL Username");
                config.Username = Console.ReadLine();
                Console.WriteLine("Input SQL Password");
                config.Password = Console.ReadLine();
                //Console.WriteLine("Input SQL Server Version");
                //config.Version = new Version(Console.ReadLine());
                localManagement.LastConfig.AdditionalConfigs.Add("DatabaseConfig", config);
                localManagement.SaveConfig(localManagement.LastConfig);
                dbConfig = localManagement.LastConfig.GetConfig<DatabaseConfig>("DatabaseConfig");
            }
            Database.Config = dbConfig;

            //Ensure the database is created. This should also verify connection
            using (var db = new Database())
            {
                db.Database.Migrate();
                db.SaveChanges();
            }

            //Configure the service provider with all relevant and required services to be injected into other classes.
            Provider = new ServiceCollection()
                .AddSingleton(x => new DiscordShardedClient(localManagement.LastConfig.GetConfig<DscSerializable>("SocketConfig").ToConfig()))
                .AddSingleton(localManagement)
                .AddSingleton<HelpService>()
                .AddSingleton(new CommandService(localManagement.LastConfig.GetConfig<CscSerializable>("CommandConfig").ToConfig()))
                .AddSingleton<ELOEventHandler>()
                .AddSingleton<Random>()
                .AddSingleton<HttpClient>()
                .AddSingleton<ReactiveService>()
                .AddSingleton<UserService>()
                .AddSingleton<PremiumService>()
                .AddSingleton<PermissionService>()
                .AddSingleton<GameService>()
                .AddSingleton<LobbyService>()
                .AddSingleton<GameSubmissionService>()
                .AddSingleton<ELOJobs>()
                .BuildServiceProvider();

            try
            {
                //Initialize the event handler
                await Provider.GetRequiredService<ELOEventHandler>().InitializeAsync(localManagement.GetConfig().Token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            await Task.Delay(-1);
        }
    }
}
