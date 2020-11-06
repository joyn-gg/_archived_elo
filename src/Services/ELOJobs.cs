using Discord;
using Discord.WebSocket;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ELO.Extensions;

namespace ELO.Services
{
    public class ELOJobs
    {
        public ELOJobs(DiscordShardedClient client)
        {
            Client = client;

            //Setup a timer that fires every 5 minutes
            CompetitionUpdateTimer = new Timer(RunQueueChecks, null, 60000, 1000 * 60 * 5);
            CacheMethodTimer = new Timer(RunCacheMethod, null, 60000, 1000 * 60 * 5);
        }

        public DiscordShardedClient Client { get; }

        public Timer CompetitionUpdateTimer { get; }
        public Timer CacheMethodTimer { get; }

        public void RunCacheMethod(object stateInfo = null)
        {
            var _ = Task.Run(async () =>
            {
                CacheMethodTimer.Change(Timeout.Infinite, Timeout.Infinite);

                try
                {
                    foreach (var shard in Client.Shards)
                    {
                        if (shard.ConnectionState == ConnectionState.Connected)
                        {
                            foreach (var guild in shard.Guilds)
                            {
                                if (!guild.HasAllMembers)
                                {

                                    var delay = Task.Delay(5000);
                                    var completeTask = await Task.WhenAny(delay, guild.DownloadUsersAsync());

                                    if (completeTask == delay)
                                    {
                                        Console.WriteLine(
                                            $"Guild: {guild.Name} [{guild.Id} Failed to return user list in time, releasing semaphore...");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Guild: {guild.Name} [{guild.Id}] Downloaded Users");
                                        if (!guild.HasAllMembers)
                                        {
                                            Console.WriteLine(
                                                $"Guild: {guild.Name} [{guild.Id}] Does not have all members, {guild.Users.Count}/{guild.MemberCount}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    CacheMethodTimer.Change(1000 * 60 * 5, 1000 * 60 * 5);
                }
            });
        }

        public void RunQueueChecks(object stateInfo = null)
        {
            var _ = Task.Run(async () =>
            {
                using (var db = new Database())
                {
                    var now = DateTime.UtcNow;

                    var competitions = db.Competitions.AsQueryable().Where(x => x.QueueTimeout != null).ToArray();
                    var compIds = competitions.Select(x => x.GuildId).ToArray();
                    var affectedPlayers = db.QueuedPlayers.AsQueryable().Where(x => compIds.Contains(x.GuildId)).ToArray();
                    foreach (var competition in competitions)
                    {
                        var lobbyMembers = affectedPlayers.Where(x => x.GuildId == competition.GuildId).GroupBy(x => x.ChannelId).ToArray();
                        foreach (var lobby in lobbyMembers)
                        {
                            var lastGame = db.GameResults.AsQueryable().Where(x => x.GuildId == competition.GuildId && x.LobbyId == lobby.Key).OrderByDescending(x => x.GameId).FirstOrDefault();

                            // Do not remove players if the game is currently picking (since players remain in queue while picking is taking place)
                            if (lastGame != null && lastGame.GameState == GameState.Picking)
                            {
                                continue;
                            }

                            foreach (var member in lobby)
                            {
                                //Too much time has passed, user is to be removed from queue.
                                if (member.QueuedAt + competition.QueueTimeout.Value < now)
                                {
                                    //Remove player from queue
                                    db.QueuedPlayers.Remove(member);

                                    //Ensure lobby channel still exists and announce the user is removed from queue
                                    var channel = Client.GetChannel(member.ChannelId) as SocketTextChannel;
                                    if (channel != null)
                                    {
                                        try
                                        {
                                            await channel.SendMessageAsync(MentionUtils.MentionUser(member.UserId), false, $"{MentionUtils.MentionUser(member.UserId)} was removed from the queue as they have been queued for more than {competition.QueueTimeout.Value.GetReadableLength()}".QuickEmbed(Color.DarkBlue));
                                        }
                                        catch //(Exception e)
                                        {
                                            //
                                        }
                                    }
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