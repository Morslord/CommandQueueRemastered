using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandQueueRemastered.Core
{
    // Exactly one target per pickup. Reordering never splits partial stacks.
    public sealed class ItemLimiter
    {
        private readonly List<QueueEntry> entries = new List<QueueEntry>();
        private readonly HashSet<string> disabledGroups = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<string, string> groupOf;
        public ItemLimiter(Func<string, string> groupOf) { this.groupOf = groupOf; }
        public IReadOnlyList<QueueEntry> Entries => entries;
        public IReadOnlyCollection<string> DisabledGroups => disabledGroups;
        public event Action Changed;
        public bool Enabled(string group) => group != null && !disabledGroups.Contains(group);
        public void Toggle(string group)
        { if (string.IsNullOrWhiteSpace(group)) return; if (!disabledGroups.Remove(group)) disabledGroups.Add(group); Changed?.Invoke(); }
        public int? Limit(string pickup) => entries.FirstOrDefault(entry => entry.Pickup == pickup)?.Count;
        public bool Blocks(string pickup, long inventoryCount) => Enabled(groupOf(pickup)) && Limit(pickup) is int limit && inventoryCount >= limit;
        public void Add(string pickup, int amount)
        {
            var entry = entries.FirstOrDefault(candidate => candidate.Pickup == pickup);
            int count = checked((entry?.Count ?? 0) + amount);
            if (string.IsNullOrWhiteSpace(pickup) || amount <= 0 || count > QueueState.MaxCount) throw new ArgumentOutOfRangeException(nameof(amount));
            if (entry == null) entries.Add(new QueueEntry(pickup, count)); else entry.Count = count;
            Changed?.Invoke();
        }
        public void Remove(string pickup, int amount)
        {
            var entry = entries.FirstOrDefault(candidate => candidate.Pickup == pickup);
            if (entry == null || amount <= 0) return;
            if (amount >= entry.Count) entries.Remove(entry); else entry.Count -= amount;
            Changed?.Invoke();
        }
        public void MoveToBoundary(int source, int boundary)
        {
            if (source < 0 || source >= entries.Count || boundary < 0 || boundary > entries.Count) throw new ArgumentOutOfRangeException();
            if (boundary == source || boundary == source + 1) return;
            var entry = entries[source]; entries.RemoveAt(source);
            entries.Insert(boundary > source ? boundary - 1 : boundary, entry); Changed?.Invoke();
        }
        public void Sort(string group, Func<string, int> idOf)
        {
            var ordered = new Queue<QueueEntry>(entries.Where(entry => groupOf(entry.Pickup) == group).OrderBy(entry => idOf(entry.Pickup)));
            for (int index = 0; index < entries.Count; index++)
                if (groupOf(entries[index].Pickup) == group) entries[index] = ordered.Dequeue();
            Changed?.Invoke();
        }
        public void Clear(string group = null)
        { entries.RemoveAll(entry => group == null || groupOf(entry.Pickup) == group); Changed?.Invoke(); }
        public List<QueueEntry> Snapshot() => entries.Select(entry => entry.Copy()).ToList();
        public void Replace(IEnumerable<QueueEntry> source, IEnumerable<string> disabled = null)
        {
            var prepared = new ItemLimiter(groupOf);
            foreach (var entry in source ?? Array.Empty<QueueEntry>())
            {
                if (entry == null || prepared.Limit(entry.Pickup).HasValue) throw new ArgumentException("Duplicate or missing limiter entry.");
                prepared.Add(entry.Pickup, entry.Count);
            }
            var groups = (disabled ?? Array.Empty<string>()).ToArray();
            if (groups.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Empty limiter group.");
            entries.Clear(); entries.AddRange(prepared.entries); disabledGroups.Clear(); disabledGroups.UnionWith(groups); Changed?.Invoke();
        }
        public void Reset() { entries.Clear(); disabledGroups.Clear(); Changed?.Invoke(); }
    }
}
