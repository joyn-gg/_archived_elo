using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class Rank
    {
        [ForeignKey("GuildId")]
        public virtual Competition Competition { get; set; }
        public ulong GuildId { get; set; }

        [Key]
        public ulong RoleId { get; set; }
        public int Points { get; set; }

        public int? WinModifier { get; set; }

        private int? _LossModifier;
        public int? LossModifier
        {
            get
            {
                return _LossModifier;
            }
            set
            {
                if (value.HasValue)
                {
                    _LossModifier = Math.Abs(value.Value);
                }
                else
                {
                    _LossModifier = value;
                }
            }
        }
    }
}
