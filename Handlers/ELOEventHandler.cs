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

            //Set commandschedule variables so they don't need to be injected
            CommandSchedule.Provider = provider;
            CommandSchedule.Service = provider.GetRequiredService<CommandService>();
        }

        public ConfigManager ConfigManager { get; }
        public Logger LogHandler { get; }

        public override async Task JoinedGuildAsync(SocketGuild guild)
        {
            //Check server whitelist
            if (!ConfigManager.LastConfig.IsAcceptable(guild.Id))
            {
                return;
            }

            //Try to find a channel the bot can send messages to with it's current permissions
            var firstChannel = guild.TextChannels.Where(x =>
            {
                var permissions = guild.CurrentUser?.GetPermissions(x);
                return permissions.HasValue ? permissions.Value.ViewChannel && permissions.Value.SendMessages : false;
            }).OrderBy(c => c.Position).FirstOrDefault();

            //Let the server know the help command name (assuming default prefix)
            await firstChannel?.SendMessageAsync("", false, new EmbedBuilder()
            {
                Title = $"{Client.CurrentUser.Username}",
                //TODO: In case that server has a custom prefix (set it, removed bot and then re-invited bot)
                //Show custom prefix instead
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
                //Still ignore messages from the bot to avoid recursive commands
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

            //Ensure the server is whitelisted or whitelist disabled
            if (!ConfigManager.LastConfig.IsAcceptable(guildId))
            {
                return;
            }

            var _ = Task.Run(async () =>
            {
                var context = new ShardedCommandContext(Client, message);
                var argPos = 0;

                if (guildId != 0 && !ConfigManager.LastConfig.Developer)
                {
                    //Check that the message was from a server and try to use a custom set prefix if available.
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
                    //If the bot is in developer mode or dms use regular prefix or dev override prefix
                    if (!message.HasStringPrefix(ConfigManager.LastConfig.Developer ? ConfigManager.LastConfig.DeveloperPrefix : ConfigManager.LastConfig.Prefix, ref argPos, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return;
                    }
                }


                //NOTE: Since guildId is 0 for dms, they have their own command queue.
                if (!CommandScheduler.ContainsKey(guildId))
                {
                    CommandScheduler[guildId] = new CommandSchedule
                    {
                        GuildId = guildId
                    };
                }

                CommandScheduler[guildId].AddTask(context, argPos);
            });
        }

    }
}

