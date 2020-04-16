using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class Player
    {
        public Player(ulong userId, ulong guildId, string displayName)
        {
            DisplayName = displayName;
            UserId = userId;
            GuildId = guildId;
            RegistrationDate = DateTime.UtcNow;
        }

        public Player() { }

        //TODO: Add display name logging
        public string DisplayName { get; set; }


        public ulong UserId { get; set; }


        [ForeignKey("GuildId")]
        public virtual Competition Competition { get; set; }
        public ulong GuildId { get; set; }

        public int Points { get; set; } = 0;

        private int _Wins = 0;

        public int Wins
        {
            get
            {
                return _Wins;
            }
            set
            {
                _Wins = NoNegative(value);
            }
        }

        private int _Losses = 0;

        public int Losses
        {
            get
            {
                return _Losses;
            }
            set
            {
                _Losses = NoNegative(value);
            }
        }

        private int _Draws = 0;

        public int Draws
        {
            get
            {
                return _Draws;
            }
            set
            {
                _Draws = NoNegative(value);
            }
        }

        /// <summary>
        /// This can be inferred from other stored data
        /// </summary>
        [NotMapped]
        public int Games => Draws + Losses + Wins;

        public DateTime RegistrationDate { get; set; }

        private int NoNegative(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value;
        }

        public string GetDisplayNameSafe()
        {
            return Discord.Format.Sanitize(DisplayName);
        }
    }
}
