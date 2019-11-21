using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Services;
using Microsoft.Extensions.DependencyInjection;
using RavenBOT.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Handlers
{
    public partial class ELOEventHandler : RavenBOT.Common.EventHandler
    {
        public ELOEventHandler(ConfigManager configManager, IServiceProvider provider) : base(provider)
        {
            //Ensure lastconfig is populated
            configManager.GetConfig();
            ConfigManager = configManager;
            LogHandler = provider.GetService<Logger>() ?? new Logger();
            Logger.Message += async (m, l) => LogHandler.Log(m, l);

            GuildSchedule.Provider = provider;
            GuildSchedule.Service = provider.GetRequiredService<CommandService>();
        }

        public ConfigManager ConfigManager { get; }
        public Logger LogHandler { get; }

        public override async Task JoinedGuildAsync(SocketGuild guild)
        {
            if (!ConfigManager.LastConfig.IsAcceptable(guild.Id))
            {
                return;
            }

            var firstChannel = guild.TextChannels.Where(x =>
            {
                var permissions = guild.CurrentUser?.GetPermissions(x);
                return permissions.HasValue ? permissions.Value.ViewChannel && permissions.Value.SendMessages : false;
            }).OrderBy(c => c.Position).FirstOrDefault();

            await firstChannel?.SendMessageAsync("", false, new EmbedBuilder()
            {
                Title = $"{Client.CurrentUser.Username}",
                Description = $"Get started by using the help command: `{ConfigManager.LastConfig.Prefix}help`",
                Color = Color.Green
            }.Build());
        }

        public override async Task MessageReceivedAsync(SocketMessage discordMessage)
        {

            if (!(discordMessage is SocketUserMessage message))
            {
                return;
            }

            if (ConfigManager.LastConfig.IgnoreBotInput)
            {
                if (message.Author.IsBot || message.Author.IsWebhook)
                {
                    return;
                }
            }
            else
            {
                if (message.Author.Id == Client.CurrentUser.Id)
                {
                    return;
                }
            }

            ulong guildId = 0;
            if (message.Channel is IGuildChannel gChannel)
            {
                guildId = gChannel.GuildId;
            }

            var _ = Task.Run(async () =>
            {
                if (!ConfigManager.LastConfig.IsAcceptable(guildId))
                {
                    return;
                }

                var context = new ShardedCommandContext(Client, message);
                var argPos = 0;

                //TODO: Add support for Custom prefixes.
                if (guildId != 0 && !ConfigManager.LastConfig.Developer)
                {
                    using (var db = new Database())
                    {
                        var comp = db.GetOrCreateCompetition(guildId);
                        var prefix = comp.Prefix ?? ConfigManager.LastConfig.Prefix;
                        if (!message.HasStringPrefix(prefix, ref argPos, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (!message.HasStringPrefix(ConfigManager.LastConfig.Developer ? ConfigManager.LastConfig.DeveloperPrefix : ConfigManager.LastConfig.Prefix, ref argPos, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return;
                    }
                }

                if (guildId != 0)
                {
                    if (!GuildScheduler.ContainsKey(guildId))
                    {
                        GuildScheduler[guildId] = new GuildSchedule
                        {
                            GuildId = guildId
                        };
                    }

                    GuildScheduler[guildId].AddTask(context, argPos);
                }
                else
                {
                    if (!GuildScheduler.ContainsKey(0))
                    {
                        GuildScheduler[0] = new GuildSchedule
                        {
                            GuildId = 0
                        };
                    }

                    GuildScheduler[0].AddTask(context, argPos);
                    //Should dm commands also just have a global queue
                    //var result = await CommandService.ExecuteAsync(context, argPos, Provider).ConfigureAwait(false);
                }
            });
        }

    }
}

