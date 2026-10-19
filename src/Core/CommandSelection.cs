using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandQueueRemastered.Core
{
    public enum ExclusiveItemBehavior { Default, Automatic }
    public sealed class CommandOption
    {
        public string Pickup { get; }
        public string Tier { get; }
        public bool Available { get; }
        public CommandOption(string pickup, string tier, bool available)
        { Pickup = pickup; Tier = tier; Available = available; }
    }

    public static class CommandSelection
    {
        // Automatic essence interaction requires a real queue entry, even in exclusive Automatic mode.
        public static bool HasQueuedChoice(QueueState queue, IReadOnlyList<CommandOption> options, string sourceTier,
            Func<string, string> tierOf, Func<string, bool> isExclusive, ExclusiveItemBehavior behavior)
        {
            int option = FindOption(queue, options, sourceTier, tierOf, isExclusive, behavior);
            return option >= 0 && FindQueueEntry(queue, options, options[option].Tier, tierOf, isExclusive) >= 0;
        }
        // The essence decides the tier. Display grouping and option ordering must never decide it.
        public static int FindOption(QueueState queue, IReadOnlyList<CommandOption> options, string sourceTier,
            Func<string, string> tierOf, Func<string, bool> isExclusive = null,
            ExclusiveItemBehavior exclusiveBehavior = ExclusiveItemBehavior.Default)
        {
            if (sourceTier == null)
            {
                // Clients may not receive the source pickup. Infer only an unambiguous item tier.
                var tiers = options.Where(option => option.Pickup != null && option.Tier != null)
                    .Select(option => option.Tier).Distinct(StringComparer.Ordinal).Take(2).ToArray();
                if (tiers.Length != 1) return -1;
                sourceTier = tiers[0];
            }
            bool exclusive = IsExclusiveEssence(options, isExclusive);
            if (exclusive)
            {
                for (int index = 0; index < options.Count; index++)
                {
                    var option = options[index];
                    if (option.Available && option.Tier == sourceTier && !queue.IsPickupPaused(option.Pickup) &&
                        (exclusiveBehavior == ExclusiveItemBehavior.Automatic ||
                         queue.FindPickup(option.Pickup, option.Tier) >= 0)) return index;
                }
                return -1;
            }
            int head = FindQueueEntry(queue, options, sourceTier, tierOf, isExclusive);
            if (head < 0 || queue.IsPickupPaused(queue.Entries[head].Pickup)) return -1;
            for (int index = 0; index < options.Count; index++)
            {
                var option = options[index];
                if (option.Available && option.Tier == sourceTier && option.Pickup == queue.Entries[head].Pickup)
                    return index;
            }
            return -1;
        }

        // Use the same pool-scoped entry for planning, validation and final consumption.
        public static int FindQueueEntry(QueueState queue, IReadOnlyList<CommandOption> options, string tier,
            Func<string, string> tierOf, Func<string, bool> isExclusive = null, bool resetRepeatCycle = false)
        {
            if (IsExclusiveEssence(options, isExclusive))
                return queue.FindPickup(options[0].Pickup, tier, resetRepeatCycle);
            // Only absent exclusive pickups may be bypassed. An offered but unavailable
            // item, or a missing regular item, must still keep the normal picker open.
            return queue.FindHead(tier, tierOf, resetRepeatCycle, pickup =>
                isExclusive == null || !isExclusive(pickup) || options.Any(option => option.Pickup == pickup));
        }

        public static bool IsExclusiveEssence(IReadOnlyList<CommandOption> options, Func<string, bool> isExclusive)
        {
            // Inspect the entire pool, including unavailable choices. A regular pool with only
            // one unlocked option must never become an exclusive essence.
            if (isExclusive == null || options.Count == 0) return false;
            string pickup = options[0].Pickup;
            return pickup != null && isExclusive(pickup) && options.All(option => option.Pickup == pickup);
        }
    }
}
