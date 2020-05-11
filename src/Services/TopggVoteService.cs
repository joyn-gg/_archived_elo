using DiscordBotsList.Api.Objects;
using Passive.Discord.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ELO.Services
{
    public class TopggVoteService
    {
        public TopggVoteService(string token, int maxRegLimit, Config config)
        {
            Token = token;
            MaxRegLimit = maxRegLimit;
            Config = config;
        }

        public string Token { get; }

        public int MaxRegLimit { get; }

        public Config Config { get; }

        public DiscordBotsList.Api.AuthDiscordBotListApi TopClient { get; private set; } = null;

        private static readonly SemaphoreSlim Locker = new SemaphoreSlim(1, 1);

        public enum ResultType
        {
            Voted,

            NotVoted,

            NotConfigured
        }

        private bool TryInitialize(Discord.WebSocket.DiscordShardedClient bot)
        {
            if (Token == null) return false;
            if (bot.CurrentUser == null) return false;
            if (TopClient == null)
            {
                TopClient = new DiscordBotsList.Api.AuthDiscordBotListApi(bot.CurrentUser.Id, Token);
            }

            return true;
        }

        public async Task SubmitGuildCountsAsync(Discord.WebSocket.DiscordShardedClient bot)
        {
            if (!TryInitialize(bot)) return;

            try
            {
                int firstShard = bot.Shards.Min(x => x.ShardId);

                // NOTE: This is only a workaround for sharding, will need to be sorted out IF multi-process sharding is to be used.
                var orderedShards = bot.Shards.OrderBy(x => x.ShardId).ToArray();
                int[] guildCounts = orderedShards.Select(x => x.Guilds.Count).ToArray();

                // Try pulling shard count from config to avoid issues when processing multiple shards.
                int shardCount = int.Parse(Config.GetOptional(Config.Defaults.ShardCount.ToString(), bot.Shards.Count.ToString()));

                await TopClient.UpdateStats(firstShard, shardCount, guildCounts);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private int checkCounter = 5;

        public async Task<ResultType> CheckAsync(Discord.WebSocket.DiscordShardedClient bot, ulong userId)
        {
            if (!TryInitialize(bot)) return ResultType.NotConfigured;

            await Locker.WaitAsync();
            try
            {
                if (await TopClient.HasVoted(userId))
                {
                    // Every 5 votes, update the guild counter.
                    checkCounter++;

                    if (checkCounter >= 5)
                    {
                        await SubmitGuildCountsAsync(bot);
                        checkCounter = 0;
                    }

                    return ResultType.Voted;
                }

                return ResultType.NotVoted;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return ResultType.NotConfigured;
            }
            finally
            {
                Locker.Release();
            }
        }
    }
}