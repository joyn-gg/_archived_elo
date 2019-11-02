using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ELO.EF.Models
{

    public class Ban
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BanId { get; set; }

        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }

        public string Comment { get; set; }
        public ulong Moderator { get; set; }
        public TimeSpan Length { get; set; }
        public DateTime TimeOfBan { get; set; }

        [NotMapped]
        public DateTime ExpiryTime => TimeOfBan + Length;

        [NotMapped]
        public TimeSpan RemainingTime => ExpiryTime - DateTime.UtcNow;

        [NotMapped]
        public bool IsExpired => ManuallyDisabled ? true : ExpiryTime < DateTime.UtcNow;
        public bool ManuallyDisabled { get; set; } = false;
    }
}
