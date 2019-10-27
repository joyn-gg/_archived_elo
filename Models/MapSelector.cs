using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenBOT.ELO.Modules.Models
{
    public class MapSelector
    {
        public enum MapMode
        {
            //Randomly select a map to play
            Random,
            //Maps iterate through. Adding/removing maps should reset or change the cycle.
            Cycle,
            //Gives an option to play the previous map, or two other maps. Previous map should not be able to be played more than twice in a row.
            //Consider edge cases (only 1 map etc.)
            Vote
        }
        public HashSet<string> Maps { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        public MapMode Mode { get; set; } = MapMode.Random;

        public List<string> History { get; set; } = new List<string>(5);

        public List<string> GetHistory()
        {
            return History;
        }

        public void AddToHistory(string map)
        {
            if (History.Count >= 5)
            {
                History.RemoveAt(0);
            }

            History.Add(map);
        }

        public (string, string, string) GenerateVoter(Random rnd)
        {
            throw new NotImplementedException();
            
            if (Mode != MapSelector.MapMode.Vote)
            {
                throw new InvalidOperationException("MakeVote is only used for vote mode.");
            }

            if (Maps.Count < 3)
            {
                return (null, null, null);
            }

            var prevMap = History.LastOrDefault();
            if (prevMap == null)
            {
                prevMap = Maps.First();
            }

            var mapRnd = Maps.Where(x => x != prevMap).OrderByDescending(x => rnd.Next()).ToArray();
            var mapA = mapRnd.First();
            var mapB = mapRnd.Last();

            return (prevMap, mapA, mapB);
        }

        public string RandomMap(Random rnd, bool addHistory)
        {
            var mapRnd = Maps.OrderByDescending(x => rnd.Next()).ToArray();
            var map = mapRnd.First();
            if (addHistory)
            {
                AddToHistory(map);
            }
            return map;
        }

        /// <summary>
        /// Gets the next map in the cycle
        /// </summary>
        /// <param name="addHistory">If the map is to be added to the history list</param>
        /// <returns></returns>
        public string NextMap(bool addHistory)
        {
            string map = null;
            if (Mode != MapSelector.MapMode.Cycle)
            {
                throw new InvalidOperationException("NextMap is only used for cycle mode.");
            }

            //Retrieve the most recent map in history.
            var lastMap = History.LastOrDefault();
            //Ensure that history actually contains maps.
            if (lastMap != null)
            {
                bool isNext = false;
                for (int i = 0; i < Maps.Count; i++)
                {
                    var mapName = Maps.ElementAt(i);
                    if (isNext)
                    {
                        map = mapName;
                        isNext = false;
                        break;
                    }
                    if (mapName == lastMap)
                    {
                        isNext = true;
                    }
                }

                if (isNext)
                {
                    //As there was no selected map (last element was reached)
                    //Use the first map in the cycle
                    map = Maps.FirstOrDefault();
                }
            }
            else
            {
                //Initialize history with the first map.
                map = Maps.FirstOrDefault();
            }

            if (map != null && addHistory)
            {
                AddToHistory(map);
            }
            return map;
        }
    }
}