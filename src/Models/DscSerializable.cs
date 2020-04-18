using Discord;
using Discord.WebSocket;

namespace ELO.Models
{
    public class DscSerializable
    {
        public bool AlwaysDownloadUsers { get; set; } = false;
        public int MessageCacheSize { get; set; } = 50;
        public LogSeverity LogLevel { get; set; } = LogSeverity.Info;
        public bool ExclusiveBulkDelete { get; set; } = true;
        public int Shards { get; set; } = 1;
        public int? ShardId { get; set; } = null;

        public DiscordSocketConfig ToConfig()
        {
            return new DiscordSocketConfig
            {
                AlwaysDownloadUsers = this.AlwaysDownloadUsers,
                MessageCacheSize = this.MessageCacheSize,
                LogLevel = this.LogLevel,
                ExclusiveBulkDelete = this.ExclusiveBulkDelete,
                ShardId = this.ShardId,

                //You may want to edit the shard count as the bot grows more and more popular.
                //Discord will block single shards that try to connect to more than 2500 servers
                //May be advisable to fetch from a config file OR default to 1
                TotalShards = this.Shards
            };
        }
    }
}