using ELO.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class GameVote
    {
        public int GameId { get; set; }

        [ForeignKey("GuildId")]
        public virtual Competition Competition { get; set; }
        public ulong GuildId { get; set; }

        [ForeignKey("ChannelId")]
        public virtual Lobby Lobby { get; set; }
        public ulong ChannelId { get; set; }
        public ulong UserId { get; set; }

        public VoteState UserVote { get; set; }

        public virtual GameResult Game { get; set; }
    }
}
