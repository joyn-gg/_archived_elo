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
        public Dictionary<ulong, CommandSchedule> CommandScheduler = new Dictionary<ulong, CommandSchedule>();
        public class CommandSchedule
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

                //Command is synchronous so add to guild queue
                Queue.Enqueue((message, argPos));

                //Check and attempt to run the command processor
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
                    //Continue getting commands until there are none left
                    while (Queue.TryDequeue(out var context))
                    {
                        try
                        {
                            //Wait for either the command to finish or 30 seconds to pass.
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
                    //Ensure running is set to false on exit so commands are not lost
                    Running = false;
                }
            }
        }

    }
}
