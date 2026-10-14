using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandQueueRemastered.Core
{
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
        // The essence decides the tier. Display grouping and option ordering must never decide it.
        public static int FindOption(QueueState queue, IReadOnlyList<CommandOption> options, string sourceTier,
            Func<string, string> tierOf)
        {
            if (sourceTier == null)
            {
                // Clients may not receive the source pickup. Infer only an unambiguous item tier.
                var tiers = options.Where(option => option.Pickup != null && option.Tier != null)
                    .Select(option => option.Tier).Distinct(StringComparer.Ordinal).Take(2).ToArray();
                if (tiers.Length != 1) return -1;
                sourceTier = tiers[0];
            }
            int head = queue.FindHead(sourceTier, tierOf, resetRepeatCycle: false);
            if (head < 0) return -1;
            for (int index = 0; index < options.Count; index++)
            {
                var option = options[index];
                if (option.Available && option.Tier == sourceTier && option.Pickup == queue.Entries[head].Pickup)
                    return index;
            }
            return -1;
        }
    }
}
