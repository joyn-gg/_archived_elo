using CommandLine;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Handlers;
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

        public async Task RunAsync(string[] args = null)
        {
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

            //Configure the service provider with all relevant and required services to be injected into other classes.
            Provider = new ServiceCollection()
                .AddSingleton(x => new DiscordShardedClient(localManagement.LastConfig.GetConfig<DscSerializable>("SocketConfig")?.ToConfig() ?? new DiscordSocketConfig
                {
                    AlwaysDownloadUsers = false,
                    MessageCacheSize = 50,
                    LogLevel = LogSeverity.Info,
                    ExclusiveBulkDelete = true,

                    //You may want to edit the shard count as the bot grows more and more popular.
                    //Discord will block single shards that try to connect to more than 2500 servers
                    //May be advisable to fetch from a config file OR default to 1
                    TotalShards = 1
                }))
                .AddSingleton(localManagement)
                .AddSingleton<HelpService>()
                .AddSingleton(new CommandService(localManagement.LastConfig.GetConfig<CscSerializable>("CommandConfig")?.ToConfig() ?? new CommandServiceConfig
                {
                    ThrowOnError = false,
                    CaseSensitiveCommands = false,
                    IgnoreExtraArgs = false,
                    DefaultRunMode = RunMode.Async,
                    LogLevel = LogSeverity.Info
                }))
                .AddSingleton<ELOEventHandler>()
                .AddSingleton<Random>()
                .AddSingleton<HttpClient>()
                .BuildServiceProvider();

            try
            {
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
