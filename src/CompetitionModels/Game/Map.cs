using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class Map
    {
        public string MapName { get; set; }

        public ulong ChannelId { get; set; }

        [ForeignKey("ChannelId")]
        public virtual Lobby Lobby { get; set; }
    }
}
