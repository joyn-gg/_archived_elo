using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;

namespace ELO.Handlers
{
    public class ELOEventHandler : RavenBOT.Common.EventHandler
    {
        public ELOEventHandler(ConfigManager configManager, Logger logHandler, IServiceProvider provider) : base(provider)
        {
            //Ensure lastconfig is populated
            configManager.GetConfig();
            ConfigManager = configManager;
            Logger.Message += (m, l) => logHandler.Log(m, l);
        }

        public ConfigManager ConfigManager { get; }

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
                var result = await CommandService.ExecuteAsync(context, argPos, Provider);
            }
        }

        /* Note, all commands must be written in order to make sure there are no guild independent race conditions due to this */
        public Dictionary<ulong, GuildSchedule> GuildScheduler = new Dictionary<ulong, GuildSchedule>();

        public class GuildSchedule
        {
            public static CommandService Service;
            public static IServiceProvider Provider;
            public ulong GuildId;
            private Queue<(ICommandContext, int)> Queue = new Queue<(ICommandContext, int)>();

            //Message should already be verified to have a proper prefix at this point.
            public void AddTask(ICommandContext message, int argPos)
            {
                var commandMatch = Service.Search(message, argPos);
                if (commandMatch.IsSuccess)
                {
                    //Ensure none of the possible commands are syncrhronous
                    if (!commandMatch.Commands.Any(x => x.Command.RunMode == RunMode.Sync))
                    {
                        //If command is async then swap in a task and return
                        var _ = Task.Run(() => Service.ExecuteAsync(message, argPos, Provider));
                        return;
                    }
                }

                Queue.Enqueue((message, argPos));

                if (ProcessorTask == null)
                {
                    ProcessorTask = Task.Run(() => RunProcessor());
                }
            }

            public Task ProcessorTask = null;
            public async Task RunProcessor()
            {
                try
                {
                    var context = Queue.Dequeue();

                    //Continue getting the next item in queue until there are none left.
                    while (context != default)
                    {
                        await Service.ExecuteAsync(context.Item1, context.Item2, Provider);

                        if (ProcessorTask.IsCanceled)
                        {
                            ProcessorTask = null;
                            return;
                        }

                        context = Queue.Dequeue();
                    }
                }
                finally
                {
                    await Task.Delay(1000);
                    await RunProcessor();
                }
            }
        }
    }
}
