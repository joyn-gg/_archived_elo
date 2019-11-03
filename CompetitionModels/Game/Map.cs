using ELO.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ELO.Models
{
    public class Map
    {
        public string MapName { get; set; }

        public ulong ChannelId { get; set; }

        [ForeignKey("ChannelId")]
        public Lobby Lobby { get; set; }
    }
}
