using System;
using System.Collections.Generic;
using System.Text;

namespace ELO.EF.Models
{
    public class Player
    {

        public string GetDisplayNameSafe()
        {
            return Discord.Format.Sanitize(DisplayName);
        }

        //TODO: Add display name logging
        public string DisplayName { get; set; }

        /// <summary>
        /// The user ID
        /// </summary>
        /// <value></value>
        public ulong UserId { get; set; }

        /// <summary>
        /// The server ID
        /// </summary>
        /// <value></value>
        public ulong GuildId { get; set; }

        public Player(ulong userId, ulong guildId, string displayName)
        {
            this.DisplayName = displayName;
            this.UserId = userId;
            this.GuildId = guildId;
            this.RegistrationDate = DateTime.UtcNow;
        }

        public Player() { }

        public int Points { get; set; } = 0;

        public void SetPoints(bool allowNegative, int points)
        {
            if (allowNegative)
            {
                Points = points;
            }
            else
            {
                Points = NoNegative(points);
            }
        }

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

        public enum ModifyState
        {
            Modify,
            Set
        }
    }
}
