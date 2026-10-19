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
        public DropCandidate(long id, long permanentItems, bool paused = false)
        { Id = id; PermanentItems = permanentItems; Paused = paused; }
    }

    // Each actual item tier has an independent cursor. Stable IDs survive joins/departures.
    public sealed class EssenceDistribution
    {
        private readonly Dictionary<string, long> lastRecipient = new Dictionary<string, long>(StringComparer.Ordinal);
        public void Reset() => lastRecipient.Clear();
        public long? Choose(string tier, EssenceDropMode mode, IEnumerable<DropCandidate> players, long? killer, bool excludePaused = false)
        {
            if (mode == EssenceDropMode.Original) return null;
            var candidates = players.Where(player => !excludePaused || !player.Paused).OrderBy(player => player.Id).ToArray();
            if (candidates.Length == 0) return null;
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
