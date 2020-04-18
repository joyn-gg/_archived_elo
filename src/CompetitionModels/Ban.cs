using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class Ban
    {
        private TimeSpan length;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BanId { get; set; }

        [ForeignKey("GuildId")]
        public virtual Competition Competition { get; set; }

        public ulong GuildId { get; set; }

        public ulong UserId { get; set; }

        public string Comment { get; set; }

        public ulong Moderator { get; set; }

        public TimeSpan Length
        {
            get
            {
                if (length > TimeSpan.FromDays(9999))
                {
                    return TimeSpan.FromDays(9999);
                }
                else
                {
                    return length;
                }
            }
            set
            {
                if (value > TimeSpan.FromDays(9999))
                {
                    length = TimeSpan.FromDays(9999);
                }
                else
                {
                    length = value;
                }
            }
        }

        public DateTime TimeOfBan { get; set; }

        [NotMapped]
        public DateTime ExpiryTime => TimeOfBan + Length;

        [NotMapped]
        public TimeSpan RemainingTime => DateTime.UtcNow > ExpiryTime ? TimeSpan.Zero : (ExpiryTime - DateTime.UtcNow);

        [NotMapped]
        public bool IsExpired => ManuallyDisabled ? true : ExpiryTime < DateTime.UtcNow;

        public bool ManuallyDisabled { get; set; } = false;
    }
}