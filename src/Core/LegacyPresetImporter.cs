using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CommandQueueRemastered.Core
{
    public static class LegacyPresetImporter
    {
        public static List<QueuePreset> Parse(IEnumerable<string> lines)
        {
            var presets = new List<QueuePreset>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int lineNumber = 0;
            foreach (string line in lines)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] fields = line.Split('|');
                if (fields.Length != 4 || string.IsNullOrWhiteSpace(fields[0]) || !ids.Add(fields[0]) ||
                    string.IsNullOrWhiteSpace(fields[1]) ||
                    !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    throw new InvalidDataException("Invalid legacy preset metadata at line " + lineNumber + ".");
                var document = new QueueDocument();
                string[] entries = fields[2].Split(',');
                for (int index = 0; index < entries.Length; index++)
                {
                    // The old writer appends a comma, including after the final entry.
                    if (index == entries.Length - 1 && entries[index].Length == 0) continue;
                    string[] item = entries[index].Split('*');
                    if (item.Length != 2 || !item[0].StartsWith("ItemIndex.", StringComparison.Ordinal) ||
                        item[0].Length == "ItemIndex.".Length ||
                        !int.TryParse(item[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count <= 0)
                        throw new InvalidDataException("Invalid legacy queue entry at line " + lineNumber + ".");
                    // Keep stable pickup names, even for items unavailable in the current catalog.
                    // Split large old stacks without truncating quantities or changing order.
                    while (count > 0)
                    {
                        int stack = Math.Min(count, QueueState.MaxCount);
                        document.Queue.Add(new QueueEntry(item[0], stack));
                        count -= stack;
                    }
                }
                QueueDocument.Decode(document.Encode());
                presets.Add(new QueuePreset { Name = fields[1], Document = document });
            }
            return presets;
        }
    }
}
