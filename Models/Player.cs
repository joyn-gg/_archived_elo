using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenBOT.ELO.Modules.Models
{
    public class Player
    {
        public static string DocumentName(ulong guildId, ulong userId)
        {
            return $"Player-{guildId}-{userId}";
        }

        /// <summary>
        /// The user display name
        /// </summary>
        /// <value></value>
        private string _DisplayName;

        public string GetDisplayNameSafe()
        {
            return Discord.Format.Sanitize(_DisplayName);
        }

        public string DisplayName
        {
            get
            {
                return _DisplayName;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                _DisplayName = value;
                NameLog.Add(DateTime.UtcNow.Ticks, value);
            }
        }

        public Dictionary<long, string> NameLog { get; set; } = new Dictionary<long, string>();

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

        public List<Ban> BanHistory { get; set; } = new List<Ban>();
        public Ban CurrentBan => BanHistory.LastOrDefault();
        public bool IsBanned => CurrentBan == null ? false : !CurrentBan.IsExpired;

        public class Ban
        {
            public Ban(TimeSpan length, ulong moderator, string comment = null)
            {
                Moderator = moderator;
                Length = length;
                TimeOfBan = DateTime.UtcNow;
                Comment = comment;
            }

            public Ban() {}
            public string Comment { get; set; }
            public ulong Moderator { get; set; }
            public TimeSpan Length { get; set; }
            public DateTime TimeOfBan { get; set; }
            public DateTime ExpiryTime => TimeOfBan + Length;
            public TimeSpan RemainingTime => ExpiryTime - DateTime.UtcNow;
            public bool IsExpired => ManuallyDisabled ? true : ExpiryTime < DateTime.UtcNow;
            public bool ManuallyDisabled { get; set; } = false;
        }

        /// <summary>
        /// Indicates the user's points.
        /// This is the primary value used to rank users.
        /// </summary>
        /// <value></value>
        private int _points = 0;
        public int Points 
        { 
            get
            {
                return _points; 
            } 
            set 
            {
                //Unfortunately there isn't an efficient way to check if the competition is 'no-negative' and I cannot make this a readonly value as it would cause issues with LiteDB.
                //All code that assigns points should run through the SetPoints function
                _points = value;
            }
        }

        public void SetPoints(CompetitionConfig comp, int points)
        {
            if (comp.AllowNegativeScore)
            {
                _points = points;
            }
            else
            {
                _points = NoNegative(points);
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

        private Queue<int> _RecentGames = new Queue<int>();
        public int[] GetRecentGames()
        {
            return _RecentGames.ToArray();
        }
        public void AddGame(int gameNumber)
        {
            _RecentGames.Enqueue(gameNumber);
            if (_RecentGames.Count > 5)
            {
                _RecentGames.Dequeue();
            }
        }

        public DateTime RegistrationDate { get; set; }
        public long RegistrationDateValue => RegistrationDate.Ticks;

        /// <summary>
        /// A set of additional integer values that can be defined in the current server.
        /// </summary>
        /// <typeparam name="string">The property name</typeparam>
        /// <typeparam name="int">The value</typeparam>
        /// <returns></returns>
        public Dictionary<string, int> AdditionalProperties { get; set; } = new Dictionary<string, int>();

        public (int, int) UpdateValue(string key, ModifyState state, int modifier)
        {
            int original = 0;
            int newVal = 0;

            //TODO: Test the matching of default to the key (for case-insensitive search)
            //TODO: Recognise points/wins/losses/draws etc. and update the actual values

            //TODO: Potentially add option to disable negative values for each additional property rather than just points specifically
            var valMatch = AdditionalProperties.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
            if (!valMatch.Equals(default(KeyValuePair<string, int>)))
            {
                newVal = ModifyValue(state, valMatch.Value, modifier);
                AdditionalProperties[valMatch.Key] = newVal;
                original = valMatch.Value;
            }
            else
            {
                newVal = ModifyValue(state, 0, modifier);
                AdditionalProperties.Add(key, newVal);
            }
            return (original, newVal);
        }

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
            Add,
            Subtract,
            Set
        }

        public static int ModifyValue(ModifyState state, int currentAmount, int modifyAmount)
        {
            switch (state)
            {
                case ModifyState.Add:
                    return currentAmount + modifyAmount;
                case ModifyState.Subtract:
                    return currentAmount - Math.Abs(modifyAmount);
                case ModifyState.Set:
                    return modifyAmount;
                default:
                    throw new ArgumentException("Provided modifystate is not valid.");
            }
        }
    }
}