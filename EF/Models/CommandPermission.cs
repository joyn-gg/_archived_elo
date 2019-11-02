using ELO.Preconditions;

namespace ELO.EF.Models
{
    public class CommandPermission
    {
        public ulong GuildId { get; set; }
        public string ComandName { get; set; }
        public RequirePermission.PermissionLevel Level { get; set; }
    }
}
