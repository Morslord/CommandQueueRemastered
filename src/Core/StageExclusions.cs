using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandQueueRemastered.Core
{
    public static class StageExclusions
    {
        public const string Defaults = "bazaar, computationalexchange";
        public static bool IsSelectable(string scene, bool gameplayScene) => gameplayScene && !string.IsNullOrWhiteSpace(scene) &&
            !string.Equals(scene, "artreview", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scene, "testotherbrother", StringComparison.OrdinalIgnoreCase);
        public static string DisplayName(string name)
        {
            name = (name ?? "").Trim().Replace('’', '\'');
            const string prefix = "Hidden Realm:";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(prefix.Length).Trim();
            const string compactPrefix = "HiddenRealm:";
            if (name.StartsWith(compactPrefix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(compactPrefix.Length).Trim();
            return name;
        }
        public static HashSet<string> Parse(string value) => new HashSet<string>((value ?? "")
            .Split(new[] { ',', ';', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        public static string Encode(IEnumerable<string> scenes) => string.Join(", ", scenes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        public static bool ContainsAll(ISet<string> selected, IEnumerable<string> sceneNames) => sceneNames.All(selected.Contains);
        public static void Set(ISet<string> selected, IEnumerable<string> sceneNames, bool exclude)
        { foreach (string scene in sceneNames) { if (exclude) selected.Add(scene); else selected.Remove(scene); } }
    }
}
