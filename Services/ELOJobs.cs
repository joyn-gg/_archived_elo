using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using RavenBOT.Common;
using Discord;

namespace ELO.Services
{
    public class ELOJobs
    {
        public ELOJobs(DiscordShardedClient client)
        {
            Client = client;
            CompetitionUpdateTimer = new Timer(RunQueueChecks, null, 0, 1000 * 30 * 1);
        }

        public DiscordShardedClient Client { get; }

        public Timer CompetitionUpdateTimer { get; }
        public void RunQueueChecks(object stateInfo = null)
        {
            var _ = Task.Run(async () =>
            {
                using (var db = new Database())
                {
                    var now = DateTime.UtcNow;
                    var queuedPlayers = db.QueuedPlayers.ToArray();
                    var guildGroups = queuedPlayers.GroupBy(x => x.GuildId);
                    foreach  (var group in guildGroups)
                    {
                        var comp = db.GetOrCreateCompetition(group.Key);
                        if (comp.QueueTimeout == null) continue;

                        foreach (var player in group)
                        {
                            //Too much time has passed, user is to be removed from queue.
                            if (player.QueuedAt + comp.QueueTimeout.Value < now)
                            {
                                db.QueuedPlayers.Remove(player);
                                var channel = Client.GetChannel(player.ChannelId) as SocketTextChannel;
                                if (channel != null)
                                {
                                    await channel.SendMessageAsync("", false, $"{MentionUtils.MentionUser(player.UserId)} was removed from the queue as they have been queued for more than {comp.QueueTimeout.Value.GetReadableLength()}".QuickEmbed(Color.DarkBlue));
                                }
                            }
                        }
                    }
                    db.SaveChanges();
                }
            });
        }
    }
}
