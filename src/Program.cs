using CommandLine;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Handlers;
using ELO.Models;
using ELO.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Passive.Discord.Setup;
using RavenBOT.Common;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ELO
{
    public class Program
    {
        /*
        public class Options
        {
            [Option('p', "path", Required = false, HelpText = "Path to a LocalConfig.json file")]
            public string Path { get; set; }
        }
        */

        public IServiceProvider Provider { get; set; }

        public static string Prefix;

        public static void Main(string[] args)
        {
            var program = new Program();
            program.RunAsync(args).GetAwaiter().GetResult();
        }

        public virtual async Task RunAsync(string[] args = null)
        {
            //Parse cl arguments and set the variables
            /*if (args != null)
            {
                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(o =>
                    {
                        if (o.Path != null)
                        {
                            ConfigManager.ConfigPath = o.Path;
                        }
                    });
            }*/
            var config = Config.ParseArguments(args);

            //Initialize new default required configs
            /*
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
            */

            Database.Serverip = config.GetOrAddEntry("DbServerIp", () =>
            {
                Console.WriteLine("DB Server IP:");
                return Console.ReadLine();
            });
            Database.Port = config.GetOrAddEntry("DbPort", () =>
            {
                Console.WriteLine("DB Server Port:");
                return Console.ReadLine();
            });
            Database.Dbname = config.GetOrAddEntry("DbName", () =>
            {
                Console.WriteLine("DB Name:");
                return Console.ReadLine();
            });
            Database.Username = config.GetOrAddEntry("DbUsername", () =>
            {
                Console.WriteLine("DB Username:");
                return Console.ReadLine();
            });
            Database.Password = config.GetOrAddEntry("DbPassword", () =>
            {
                Console.WriteLine("DB Password:");
                return Console.ReadLine();
            });
            Prefix = config.GetOrAddEntry("Prefix", () =>
            {
                Console.WriteLine("Bot prefix:");
                return Console.ReadLine();
            });

            var logLevel = (Discord.LogSeverity)Enum.Parse(typeof(Discord.LogSeverity), config.GetOptional("LogLevel", "Info"));

            // bool.Parse(config.GetOptional("CaseSensitiveCommands", "false"))
            var commandConfig = new CommandServiceConfig()
            {
                CaseSensitiveCommands = false,
                ThrowOnError = false,
                IgnoreExtraArgs = false,
                DefaultRunMode = RunMode.Async,
                LogLevel = logLevel
            };

            var shardCount = config.GetOptional("ShardCount", "1");
            var shardId = config.GetOptional("ShardId", null);
            if (shardCount == null && shardId == null)
            {
                throw new Exception("No shard configuration provided.");
            }

            int? shardCountValue = null;
            if (shardCount != null) shardCountValue = int.Parse(shardCount);
            int? shardIdValue = null;
            if (shardId != null) shardIdValue = int.Parse(shardId);

            var socketConfig = new DiscordSocketConfig()
            {
                TotalShards = shardCountValue,
                ShardId = shardIdValue,
                LogLevel = logLevel,
                ExclusiveBulkDelete = true,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 10
            };

            var token = config.GetOrAddEntry("Token", () =>
            {
                Console.WriteLine("Input bot token:");
                return Console.ReadLine();
            });

            var serverInvite = config.GetOrAddEntry("PremiumServerInvite", () =>
            {
                Console.WriteLine("PremiumServerInvite:");
                return Console.ReadLine();
            });
            var altLink = config.GetOrAddEntry("PremiumAltLink", () =>
            {
                Console.WriteLine("PremiumAltLink:");
                return Console.ReadLine();
            });
            var guildId = ulong.Parse(config.GetOrAddEntry("PremiumGuildId", () =>
            {
                Console.WriteLine("PremiumGuildId:");
                return Console.ReadLine();
            }));
            var premiumDeletionChannelId = ulong.Parse(config.GetOrAddEntry("PremiumDeletionWebhookChannelId", () =>
            {
                Console.WriteLine("PremiumDeletionWebhookChannelId:");
                return Console.ReadLine();
            }));
            var premiumDeletionWebhookId = ulong.Parse(config.GetOrAddEntry("PremiumDeletionWebhookClientId", () =>
            {
                Console.WriteLine("PremiumDeletionWebhookClientId: (Can be found in the webhook url)");
                return Console.ReadLine();
            }));

            //Ensure the database is created. This should also verify connection
            using (var db = new Database())
            {
                db.Database.Migrate();
                db.SaveChanges();
            }

            var collection = new ServiceCollection()
                            .AddSingleton(new DiscordShardedClient(socketConfig))
                            .AddSingleton(config)
                            .AddSingleton(new PremiumService.Config
                            {
                                ServerInvite = serverInvite,
                                AltLink = altLink,
                                GuildId = guildId,
                                DeletionWebhookChannel = premiumDeletionChannelId,
                                DeletionWebhookClientId = premiumDeletionWebhookId
                            })
                            .AddSingleton<HelpService>()
                            .AddSingleton(new CommandService(commandConfig))
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
                            .AddSingleton<ELOJobs>();

            var topggToken = config.GetOptional("TopGgToken", null);
            int voteLimit = 40;
            if (topggToken != null)
            {
                voteLimit = int.Parse(config.GetOptional("RegVoteLimit", "40"));
            }
            collection.AddSingleton(new TopggVoteService(topggToken, voteLimit, config));

            //Configure the service provider with all relevant and required services to be injected into other classes.
            Provider = collection.BuildServiceProvider();

            try
            {
                //Initialize the event handler
                await Provider.GetRequiredService<ELOEventHandler>().InitializeAsync(token);
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