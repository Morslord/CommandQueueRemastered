using System;
using System.Linq;
using System.Reflection;
using RoR2;

namespace CommandQueueRemastered.Integrations
{
    internal sealed class ItemDescriptions
    {
        private readonly RemasteredPlugin plugin;
        private MethodInfo enhanced;
        private float nextLookup;
        private bool failed;
        private bool reportedMismatch;
        public ItemDescriptions(RemasteredPlugin plugin)
        {
            this.plugin = plugin;
            FindIntegration();
        }
        private void FindIntegration()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("LookingGlass.ItemStatsNameSpace.ItemStats"))
                .FirstOrDefault(candidate => candidate != null);
            enhanced = type?.GetMethod("GetItemDescription", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(ItemDef), typeof(int), typeof(CharacterMaster), typeof(bool), typeof(bool) }, null);
            if (type != null && enhanced == null && !reportedMismatch)
            { reportedMismatch = true; plugin.LogWarning("LookingGlass API differs; using vanilla descriptions."); }
        }
        public string Get(ItemDef item, CharacterMaster master)
        {
            if (enhanced == null && UnityEngine.Time.unscaledTime >= nextLookup)
            {
                nextLookup = UnityEngine.Time.unscaledTime + 5f;
                FindIntegration();
            }
            if (enhanced != null && !failed && master)
            {
                try
                {
                    int count = master.inventory ? master.inventory.GetItemCountEffective(item.itemIndex) : 0;
                    string description = enhanced.Invoke(null, new object[] { item, count, master, true, false }) as string;
                    if (!string.IsNullOrEmpty(description)) return description;
                }
                catch (Exception exception) { failed = true; plugin.LogError("LookingGlass description failed; using vanilla descriptions", exception); }
            }
            string fallback = Language.GetString(item.descriptionToken);
            if (string.IsNullOrWhiteSpace(fallback) || fallback == item.descriptionToken)
                fallback = Language.GetString(item.pickupToken);
            return fallback;
        }
    }
}
