using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Handlers
{
    /// <summary>
    /// Class for firing an event when all shards are ready since this is not offered by d.net
    /// </summary>
    public class ShardChecker
    {
        //TODO: Unsub from shardReady event once allshardsfired is set to true
        public ShardChecker(DiscordShardedClient client)
        {
            client.ShardReady += ShardReadyAsync;
            Client = client;
        }

        public List<int> ReadyShardIds { get; set; } = new List<int>();
        public DiscordShardedClient Client { get; }

        public event Func<Task> AllShardsReady;

        public bool AllShardsReadyFired = false;

        public Task ShardReadyAsync(DiscordSocketClient socketClient)
        {
            if (AllShardsReadyFired)
            {
                return Task.CompletedTask;
            }

            if (!ReadyShardIds.Contains(socketClient.ShardId))
            {
                ReadyShardIds.Add(socketClient.ShardId);
            }

            if (Client.Shards.All(x => ReadyShardIds.Contains(x.ShardId)))
            {
                AllShardsReady.Invoke();
                AllShardsReadyFired = true;
            }

            return Task.CompletedTask;
        }
    }
}