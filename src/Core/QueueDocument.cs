using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CommandQueueRemastered.Core
{
    public sealed class QueueDocument
    {
        public int Version { get; set; } = 1;
        public List<QueueEntry> Queue { get; set; } = new List<QueueEntry>();
        public List<string> RepeatedTiers { get; set; } = new List<string>();

        public static QueueDocument Capture(QueueState state) => new QueueDocument
        { Queue = state.Snapshot(), RepeatedTiers = new List<string>(state.RepeatedTiers) };

        public static QueueDocument Decode(string json)
        {
            var document = JsonStorage.Decode<QueueDocument>(json);
            if (document == null || document.Version != 1 || document.Queue == null || document.RepeatedTiers == null)
                throw new InvalidDataException("Unsupported or incomplete queue document.");
            // The same validation applies to disk files and ProperSave payloads.
            new QueueState().Replace(document.Queue, document.RepeatedTiers);
            return document;
        }
        public string Encode() => JsonStorage.Encode(this);
        public void Apply(QueueState state) => state.Replace(Queue, RepeatedTiers);
        public void ApplyGroups(QueueState state, Func<string, string> pickupGroup, Func<string, string> repeatGroup)
            => state.ReplaceGroups(Queue, RepeatedTiers, pickupGroup, repeatGroup);
    }

    public sealed class QueuePreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public QueueDocument Document { get; set; }
    }

    public sealed class PresetStore
    {
        private readonly string path;
        private List<QueuePreset> presets = new List<QueuePreset>();
        private bool legacyImportCompleted;
        public IReadOnlyList<QueuePreset> Presets => presets;
        public PresetStore(string path) { this.path = path; }
        public void Load()
        {
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);
            // Read the original Remastered array as well as the import-aware container.
            bool completed = false;
            List<QueuePreset> loaded;
            if (json.TrimStart().StartsWith("[", StringComparison.Ordinal))
                loaded = JsonStorage.Decode<List<QueuePreset>>(json);
            else
            {
                var file = JsonStorage.Decode<PresetFile>(json);
                if (file == null || file.Version != 1) throw new InvalidDataException("Unsupported preset file.");
                loaded = file.Presets;
                completed = file.LegacyImportCompleted;
            }
            if (loaded == null) throw new InvalidDataException("Missing preset list.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var preset in loaded)
            {
                if (preset == null || string.IsNullOrWhiteSpace(preset.Id) || !ids.Add(preset.Id) || string.IsNullOrWhiteSpace(preset.Name) || preset.Document == null)
                    throw new InvalidDataException("Invalid preset metadata.");
                QueueDocument.Decode(preset.Document.Encode());
            }
            presets = loaded;
            legacyImportCompleted = completed;
        }
        public int ImportLegacy(string sourcePath)
        {
            if (legacyImportCompleted || !File.Exists(sourcePath)) return 0;
            // Parse everything before writing: malformed input must not cause a partial import.
            var imported = LegacyPresetImporter.Parse(File.ReadAllLines(sourcePath));
            Commit(list => list.AddRange(imported), completeLegacyImport: true);
            return imported.Count;
        }
        public void Add(string name, QueueState state)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a preset name.");
            Commit(list => list.Add(new QueuePreset { Name = name.Trim(), Document = QueueDocument.Capture(state) }));
        }
        public void Rename(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a preset name.");
            Commit(list => list.Find(preset => preset.Id == id).Name = name.Trim());
        }
        public void Delete(string id) => Commit(list => list.RemoveAll(preset => preset.Id == id));
        // Boundary is measured in the displayed list before removing the source row.
        public void MoveToBoundary(string id, int boundary)
        {
            int source = presets.FindIndex(preset => preset.Id == id);
            if (source < 0) throw new ArgumentException("Unknown preset.", nameof(id));
            if (boundary < 0 || boundary > presets.Count) throw new ArgumentOutOfRangeException(nameof(boundary));
            if (boundary == source || boundary == source + 1) return;
            Commit(list =>
            {
                var preset = list[source];
                list.RemoveAt(source);
                list.Insert(boundary > source ? boundary - 1 : boundary, preset);
            });
        }
        private void Commit(Action<List<QueuePreset>> change, bool completeLegacyImport = false)
        {
            // Publish in-memory changes only after the file replacement succeeds.
            var updated = JsonStorage.Decode<List<QueuePreset>>(JsonStorage.Encode(presets));
            change(updated);
            string temporary = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool completed = legacyImportCompleted || completeLegacyImport;
            File.WriteAllText(temporary, JsonStorage.Encode(new PresetFile
                { Version = 1, Presets = updated, LegacyImportCompleted = completed }));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
            presets = updated;
            legacyImportCompleted = completed;
        }
    }

    public sealed class PresetFile
    {
        public int Version { get; set; }
        public List<QueuePreset> Presets { get; set; }
        public bool LegacyImportCompleted { get; set; }
    }

    internal static class JsonStorage
    {
        public static string Encode<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static T Decode<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
    }
}
