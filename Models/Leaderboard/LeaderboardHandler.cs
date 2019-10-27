using MoreLinq;
using System.Collections.Generic;
using System.Linq;

namespace RavenBOT.ELO.Modules.Models.Leaderboard
{
    public class LeaderboardHandler
    {
        public int LeaderboardSize = 100;

        private Dictionary<ulong, int> _leaderboard = new Dictionary<ulong, int>();

        //NOTE: Always ignores last element as it's score does not necessarily represent the smallest score on the leaderboard
        public Dictionary<ulong, int> GetLeaderboard()
        {
            return _leaderboard.OrderByDescending(x => x.Value).Take(LeaderboardSize - 1).ToDictionary();
        }
        private ulong minKey;

        //TODO: How do 
        public void TryUpdate(ulong id, int val)
        {
            var minValue = _leaderboard[minKey];

            if (_leaderboard.ContainsKey(id))
            {
                if (val < minValue)
                {
                    _leaderboard.Remove(id);
                }
                else
                {
                    _leaderboard[id] = val;
                }
                return;
            }


            //Ensure it does not exceed max size by removing smallest score if a new one is to be added.
            if (val > minValue && _leaderboard.Count > LeaderboardSize)
            {
                _leaderboard.Remove(minKey);
                _leaderboard.Add(id, val);
                minKey = _leaderboard.OrderBy(x => x.Value).First().Key;

            }
            else
            {
                _leaderboard.Add(id, val);
            }

        }

    }
}