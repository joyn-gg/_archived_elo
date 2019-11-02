using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class CommandPermission
    {
        [ForeignKey("GuildId")]
        public Competition Competition { get; set; }
        public ulong GuildId { get; set; }
        public string ComandName { get; set; }
        public PermissionLevel Level { get; set; }
    }
}
