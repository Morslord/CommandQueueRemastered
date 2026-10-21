using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandQueueRemastered.Core
{
    public sealed class PresetPreviewItem
    {
        public string Pickup { get; }
        public long Quantity { get; }
        public int? Limit { get; }
        public bool LimiterOnly => Quantity == 0;
        public string Badge => Quantity + "/" + (Limit.HasValue ? Limit.Value.ToString() : "—");
        public PresetPreviewItem(string pickup, long quantity, int? limit) { Pickup = pickup; Quantity = quantity; Limit = limit; }
    }
    public static class PresetPreview
    {
        public static IReadOnlyList<PresetPreviewItem> Build(QueueDocument document, Func<string, int> tierOrder = null)
        {
            var quantities = document.Queue.GroupBy(entry => entry.Pickup, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(entry => (long)entry.Count), StringComparer.Ordinal);
            var limits = (document.Limits ?? new List<QueueEntry>()).ToDictionary(entry => entry.Pickup, entry => entry.Count, StringComparer.Ordinal);
            var order = document.Queue.Select(entry => entry.Pickup).Concat((document.Limits ?? new List<QueueEntry>()).Select(entry => entry.Pickup)).Distinct(StringComparer.Ordinal);
            return order.Select(pickup => new PresetPreviewItem(pickup, quantities.TryGetValue(pickup, out long count) ? count : 0,
                limits.TryGetValue(pickup, out int limit) ? (int?)limit : null))
                .OrderBy(item => tierOrder?.Invoke(item.Pickup) ?? 0).ThenBy(item => item.LimiterOnly ? 1 : 0).ToArray();
        }
    }
}
