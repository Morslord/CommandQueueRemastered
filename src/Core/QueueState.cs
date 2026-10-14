using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandQueueRemastered.Core
{
    // Stable catalog names are persisted; runtime pickup indices are never saved.
    public sealed class QueueEntry
    {
        public string Pickup { get; set; }
        public int Count { get; set; }
        public QueueEntry() { }
        public QueueEntry(string pickup, int count) { Pickup = pickup; Count = count; }
        public QueueEntry Copy() => new QueueEntry(Pickup, Count);
    }

    public sealed class QueueState
    {
        public const int MaxCount = 999;
        private readonly List<QueueEntry> entries = new List<QueueEntry>();
        private readonly HashSet<string> repeatedTiers = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<string, string> groupOf;
        private readonly Dictionary<QueueEntry, int> repeatProgress = new Dictionary<QueueEntry, int>();
        private bool rotateRepeatedItems = true;
        public bool RotateRepeatedItems
        {
            get => rotateRepeatedItems;
            set { if (rotateRepeatedItems != value) { rotateRepeatedItems = value; NotifyChanged(); } }
        }
        private void NotifyChanged() { repeatProgress.Clear(); Changed?.Invoke(); }
        public QueueState(Func<string, string> groupOf = null) { this.groupOf = groupOf ?? (_ => "Queue"); }
        public IReadOnlyList<QueueEntry> Entries => entries;
        public IReadOnlyCollection<string> RepeatedTiers => repeatedTiers;
        public event Action Changed;

        public void Enqueue(string pickup, int count = 1, bool atFront = false)
        {
            Validate(pickup, count);
            string group = groupOf(pickup);
            int neighbor = atFront ? entries.FindIndex(entry => groupOf(entry.Pickup) == group)
                : entries.FindLastIndex(entry => groupOf(entry.Pickup) == group);
            if (neighbor >= 0 && entries[neighbor].Pickup == pickup)
            {
                int total = checked(entries[neighbor].Count + count);
                if (total > MaxCount) throw new ArgumentOutOfRangeException(nameof(count), "Stack limit exceeded.");
                entries[neighbor].Count = total;
            }
            else entries.Insert(neighbor < 0 ? entries.Count : neighbor + (atFront ? 0 : 1), new QueueEntry(pickup, count));
            NotifyChanged();
        }

        public void Increase(int index, int count = 1)
        {
            int total = checked(entries[index].Count + count);
            Validate(entries[index].Pickup, total);
            entries[index].Count = total;
            NotifyChanged();
        }

        public void Remove(int index, bool wholeStack)
        {
            RemoveQuantity(index, wholeStack ? entries[index].Count : 1);
        }

        public void RemoveQuantity(int index, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count >= entries[index].Count) entries.RemoveAt(index);
            else entries[index].Count -= count;
            Normalize();
            NotifyChanged();
        }

        public void Move(int from, int to, int count = 0)
        {
            if (from < 0 || from >= entries.Count || to < 0 || to >= entries.Count)
                throw new ArgumentOutOfRangeException(nameof(to));
            if (from == to) return;
            MoveToBoundary(from, to + (from < to ? 1 : 0), count);
        }

        // The boundary is measured before removal: 0 is before the first entry, Count after the last.
        public void MoveToBoundary(int from, int boundary, int count = 0)
        {
            if (from < 0 || from >= entries.Count || boundary < 0 || boundary > entries.Count)
                throw new ArgumentOutOfRangeException(nameof(boundary));
            int amount = count == 0 ? entries[from].Count : count;
            if (amount < 0 || amount > entries[from].Count) throw new ArgumentOutOfRangeException(nameof(count));
            if (boundary == from || boundary == from + 1) return;
            var entry = new QueueEntry(entries[from].Pickup, amount);
            int insertion = boundary;
            if (amount == entries[from].Count)
            { entries.RemoveAt(from); if (from < insertion) insertion--; }
            else entries[from].Count -= amount;
            entries.Insert(insertion, entry);
            Normalize();
            NotifyChanged();
        }

        private void Normalize()
        {
            // The display groups can be interleaved in storage; merge neighbors within each displayed group.
            var lastByGroup = new Dictionary<string, QueueEntry>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                string group = groupOf(entry.Pickup) ?? entry.Pickup;
                if (lastByGroup.TryGetValue(group, out var previous) && previous.Pickup == entry.Pickup)
                {
                    int transfer = Math.Min(MaxCount - previous.Count, entry.Count);
                    previous.Count += transfer; entry.Count -= transfer;
                    if (entry.Count == 0) { entries.RemoveAt(index--); continue; }
                }
                lastByGroup[group] = entry;
            }
        }

        public void ReplaceGroups(IEnumerable<QueueEntry> source, IEnumerable<string> repeats,
            Func<string, string> pickupGroup, Func<string, string> repeatGroup)
        {
            var validation = new QueueState(); validation.Replace(source, repeats);
            var incoming = validation.Snapshot();
            var groups = new HashSet<string>(incoming.Select(entry => pickupGroup(entry.Pickup)), StringComparer.Ordinal);
            if (groups.Contains(null)) throw new ArgumentException("A saved pickup cannot be assigned to a group.");
            if (groups.Count == 0) return;
            var updated = entries.Where(entry => !groups.Contains(pickupGroup(entry.Pickup))).Select(entry => entry.Copy()).ToList();
            updated.AddRange(incoming);
            var updatedRepeats = repeatedTiers.Where(tier => !groups.Contains(repeatGroup(tier)))
                .Concat(validation.RepeatedTiers.Where(tier => groups.Contains(repeatGroup(tier)))).ToArray();
            var prepared = new QueueState(groupOf);
            prepared.Replace(updated, updatedRepeats);
            prepared.Normalize();
            Replace(prepared.Entries, updatedRepeats);
        }

        public bool IsRepeated(string tier) => repeatedTiers.Contains(tier);
        public void ToggleRepeat(string tier)
        {
            if (!repeatedTiers.Remove(tier)) repeatedTiers.Add(tier);
            NotifyChanged();
        }

        // Selection is scoped to the actual pickup tier, including within the shared Void tab.
        public int FindHead(string tier, Func<string, string> tierOf, bool resetRepeatCycle = true)
        {
            if (RotateRepeatedItems || !IsRepeated(tier)) return entries.FindIndex(entry => tierOf(entry.Pickup) == tier);
            int next = entries.FindIndex(entry => tierOf(entry.Pickup) == tier &&
                (!repeatProgress.TryGetValue(entry, out int consumed) || consumed < entry.Count));
            if (next >= 0) return next;
            // Each actual Void tier has its own cycle, even in the combined display.
            if (resetRepeatCycle)
                foreach (var entry in entries.Where(entry => tierOf(entry.Pickup) == tier)) repeatProgress.Remove(entry);
            return entries.FindIndex(entry => tierOf(entry.Pickup) == tier);
        }

        public void Consume(int index, string tier)
        {
            if (!RotateRepeatedItems && IsRepeated(tier))
            {
                var entry = entries[index];
                repeatProgress.TryGetValue(entry, out int consumed);
                repeatProgress[entry] = consumed + 1;
                Changed?.Invoke();
                return;
            }
            string pickup = entries[index].Pickup;
            if (--entries[index].Count == 0) entries.RemoveAt(index);
            if (IsRepeated(tier))
            {
                // Append before merging: the displayed group tail may precede hidden groups.
                // Normalize keeps overflow as a separate stack when the preceding stack is full.
                entries.Add(new QueueEntry(pickup, 1));
            }
            Normalize();
            // Observers see the completed rotation, never an intermediate missing item.
            NotifyChanged();
        }

        public void Clear() { entries.Clear(); NotifyChanged(); }
        public void Reset() { entries.Clear(); repeatedTiers.Clear(); NotifyChanged(); }
        public List<QueueEntry> Snapshot() => entries.Select(entry => entry.Copy()).ToList();

        public void Replace(IEnumerable<QueueEntry> source, IEnumerable<string> repeats = null)
        {
            // Validate the complete input before changing a live queue.
            if (source == null) throw new ArgumentNullException(nameof(source));
            var validated = source.Select(entry =>
            {
                if (entry == null) throw new ArgumentException("Null queue entry.");
                Validate(entry.Pickup, entry.Count);
                return entry.Copy();
            }).ToList();
            var validatedRepeats = (repeats ?? Array.Empty<string>()).ToArray();
            if (validatedRepeats.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Empty tier name.");
            entries.Clear(); entries.AddRange(validated);
            repeatedTiers.Clear(); repeatedTiers.UnionWith(validatedRepeats);
            NotifyChanged();
        }

        private static void Validate(string pickup, int count)
        {
            if (string.IsNullOrWhiteSpace(pickup)) throw new ArgumentException("Empty pickup name.", nameof(pickup));
            if (count <= 0 || count > MaxCount) throw new ArgumentOutOfRangeException(nameof(count));
        }
    }
}
