using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Handlers
{
    public partial class ELOEventHandler
    {
        public Dictionary<ulong, GuildSchedule> GuildScheduler = new Dictionary<ulong, GuildSchedule>();
        public class GuildSchedule
        {
            public static CommandService Service;
            public static IServiceProvider Provider;

            public ulong GuildId;
            private Queue<(ICommandContext, int)> Queue = new Queue<(ICommandContext, int)>();
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

                if (!Running)
                {
                    Task.Run(() => RunProcessor());
                }
            }

            public bool Running = false;
            public async Task RunProcessor()
            {
                Running = true;
                try
                {
                    while (Queue.TryDequeue(out var context))
                    {
                        try
                        {
                            Task.WaitAny(Service.ExecuteAsync(context.Item1, context.Item2, Provider), Task.Delay(30000));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    Running = false;
                }
            }
        }

    }
}
