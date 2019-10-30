using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;

namespace ELO.Handlers
{
    public class ELOEventHandler : RavenBOT.Handlers.EventHandler
    {
        public ELOEventHandler(DiscordShardedClient client, CommandService commandService, GuildService guildService, LocalManagementService local, LogHandler handler, IServiceProvider provider) : base(client, commandService, guildService, local, handler, provider)
        {
            GuildSchedule.Service = commandService;
            GuildSchedule.Provider = provider;
        }

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

        public override async Task MessageReceivedAsync(SocketMessage discordMessage)
        {
            if (!(discordMessage is SocketUserMessage message))
            {
                return;
            }

            if (LocalManagementService.LastConfig.IgnoreBotInput)
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

            if (!LocalManagementService.LastConfig.IsAcceptable(guildId))
            {
                return;
            }

            var context = new ShardedCommandContext(Client, message);
            var argPos = 0;
            if (!message.HasStringPrefix(LocalManagementService.LastConfig.Developer ? LocalManagementService.LastConfig.DeveloperPrefix : GuildService.GetPrefix(guildId), ref argPos, StringComparison.InvariantCultureIgnoreCase) /*&& !message.HasMentionPrefix(Client.CurrentUser, ref argPos)*/ )
            {
                return;
            }

            if (!GuildService.IsModuleAllowed(context.Guild?.Id ?? 0, message.Content))
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
    }
}
