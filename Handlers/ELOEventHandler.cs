using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ELO.Handlers
{
    public class ELOEventHandler : RavenBOT.Common.EventHandler
    {
        public ELOEventHandler(ConfigManager configManager, IServiceProvider provider) : base(provider)
        {
            //Ensure lastconfig is populated
            configManager.GetConfig();
            ConfigManager = configManager;
            LogHandler = provider.GetService<Logger>() ?? new Logger();
            Logger.Message += async (m, l) => LogHandler.Log(m, l);
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

            if (!ConfigManager.LastConfig.IsAcceptable(guildId))
            {
                return;
            }

            var context = new ShardedCommandContext(Client, message);
            var argPos = 0;

            //TODO: Add support for Custom prefixes.
            if (!message.HasStringPrefix(ConfigManager.LastConfig.Developer ? ConfigManager.LastConfig.DeveloperPrefix : ConfigManager.LastConfig.Prefix, ref argPos, StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }


            var result = await CommandService.ExecuteAsync(context, argPos, Provider);
        }
        
    }
}
