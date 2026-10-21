using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandQueueRemastered.Core
{
    public enum EssenceDropMode { Original, RoundRobin, Killer, FewestItems }
    public enum InventoryCountMode { AllItems, SameTier }

    public sealed class DropCandidate
    {
        public long Id { get; }
        public long PermanentItems { get; }
        public bool Paused { get; }
        public bool Ignored { get; }
        public DropCandidate(long id, long permanentItems, bool paused = false, bool ignored = false)
        { Id = id; PermanentItems = permanentItems; Paused = paused; Ignored = ignored; }
    }

    // Each actual item tier has an independent cursor. Stable IDs survive joins/departures.
    public sealed class EssenceDistribution
    {
        private readonly Dictionary<string, long> lastRecipient = new Dictionary<string, long>(StringComparer.Ordinal);
        private long? hexRecipient;
        public void Reset() { lastRecipient.Clear(); hexRecipient = null; }
        public static long? ChooseSkillUser(IEnumerable<DropCandidate> players, long? skillUser, bool excludePaused) =>
            players.FirstOrDefault(player => player.Id == skillUser && !player.Ignored && (!excludePaused || !player.Paused))?.Id;
        public long? Choose(string tier, EssenceDropMode mode, IEnumerable<DropCandidate> players, long? killer, bool excludePaused = false, bool hexGroupsOfSeven = false)
        {
            if (mode == EssenceDropMode.Original) return null;
            var candidates = players.Where(player => !player.Ignored && (!excludePaused || !player.Paused)).OrderBy(player => player.Id).ToArray();
            if (candidates.Length == 0) return null;
            if (hexGroupsOfSeven)
            {
                // Finish an incomplete block before starting another. Counts include pending drops.
                var incomplete = candidates.Where(player => player.PermanentItems % 7 != 0).ToArray();
                if (incomplete.Length > 0)
                {
                    var current = incomplete.FirstOrDefault(player => player.Id == hexRecipient);
                    var chosenHex = current ?? incomplete.OrderByDescending(player => player.PermanentItems % 7).ThenBy(player => player.Id).First();
                    hexRecipient = chosenHex.Id; lastRecipient[tier] = chosenHex.Id; return chosenHex.Id;
                }
                lastRecipient.TryGetValue(tier, out long previousHex);
                var nextHex = candidates.FirstOrDefault(player => player.Id > previousHex) ?? candidates[0];
                hexRecipient = nextHex.Id; lastRecipient[tier] = nextHex.Id; return nextHex.Id;
            }
            if (mode == EssenceDropMode.Killer && killer.HasValue && candidates.Any(player => player.Id == killer.Value))
                return killer;
            if (mode == EssenceDropMode.FewestItems)
            {
                long fewest = candidates.Min(player => player.PermanentItems);
                candidates = candidates.Where(player => player.PermanentItems == fewest).ToArray();
            }
            long previous;
            var chosen = lastRecipient.TryGetValue(tier, out previous)
                ? candidates.FirstOrDefault(player => player.Id > previous) ?? candidates[0] : candidates[0];
            lastRecipient[tier] = chosen.Id;
            return chosen.Id;
        }
    }
}
