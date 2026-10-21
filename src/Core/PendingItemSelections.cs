using System;
using System.Collections.Generic;

namespace CommandQueueRemastered.Core
{
    // A local selection may precede the replicated inventory by several frames (native transfer orb).
    public sealed class PendingItemSelections
    {
        public sealed class Ticket { internal string Pickup; internal double Expires; internal int Remaining; }
        private readonly Dictionary<string, long> observed = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly List<Ticket> pending = new List<Ticket>();
        public long Count(string pickup, long actual, double now)
        {
            pending.RemoveAll(ticket => ticket.Expires <= now);
            if (observed.TryGetValue(pickup, out long previous) && actual > previous)
            {
                long gained = actual - previous;
                for (int index = 0; index < pending.Count && gained > 0; index++)
                    if (pending[index].Pickup == pickup)
                    {
                        int accepted = (int)System.Math.Min(gained, pending[index].Remaining);
                        pending[index].Remaining -= accepted; gained -= accepted;
                        if (pending[index].Remaining == 0) pending.RemoveAt(index--);
                    }
            }
            observed[pickup] = actual;
            long count = 0; foreach (var ticket in pending) if (ticket.Pickup == pickup) count += ticket.Remaining;
            return count;
        }
        public Ticket Reserve(string pickup, long actual, double now, int amount = 1)
        {
            if (amount <= 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            Count(pickup, actual, now);
            // Rejected selections or uncollected ground items must not lock a limiter forever.
            var ticket = new Ticket { Pickup = pickup, Expires = now + 10, Remaining = amount }; pending.Add(ticket); return ticket;
        }
        public void Cancel(Ticket ticket) { if (ticket != null) pending.Remove(ticket); }
    }
}
