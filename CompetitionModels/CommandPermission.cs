using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class CommandPermission
    {
        [ForeignKey("GuildId")]
        public virtual Competition Competition { get; set; }
        public ulong GuildId { get; set; }
        public string CommandName { get; set; }
        public PermissionLevel Level { get; set; }
    }
}
