using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;

namespace CommandQueueRemastered.Game
{
    internal static class ItemGroups
    {
        public static readonly string[] Names = { "White", "Green", "Red", "Lunar", "Boss", "Void", "Hallowed", "Hex", "Pages" };
        public static string Group(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.Tier1: return "White";
                case ItemTier.Tier2: return "Green";
                case ItemTier.Tier3: return "Red";
                case ItemTier.Lunar: return "Lunar";
                case ItemTier.Boss: return "Boss";
                case ItemTier.VoidTier1: case ItemTier.VoidTier2: case ItemTier.VoidTier3: case ItemTier.VoidBoss: return "Void";
                case ItemTier.Hex: return "Hex";
                case ItemTier.Page: return "Pages";
                case ItemTier.Cursed: return "Hallowed";
                default: return null;
            }
        }

        // Hallowed is resolved from the live tier catalog, not guessed from an enum value.
        public static string Group(ItemDef item)
        {
            if (!item) return null;
            var definition = ItemTierCatalog.GetItemTierDef(item.tier);
            if (definition && definition.name.IndexOf("Hallowed", StringComparison.OrdinalIgnoreCase) >= 0) return "Hallowed";
            return Group(item.tier);
        }
        public static ItemDef Definition(string pickup)
        {
            var definition = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(pickup));
            return definition == null ? null : ItemCatalog.GetItemDef(definition.itemIndex);
        }
        public static string TierOf(string pickup) => Definition(pickup)?.tier.ToString();
        public static string GroupOf(string pickup) => Group(Definition(pickup));
        public static string GroupOfTier(string tier) => Enum.TryParse(tier, out ItemTier value) ? Group(value) : null;
        public static string PickupName(ItemDef item) => PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(item.itemIndex))?.internalName;
        public static int PreviewTierOrder(string pickup)
        {
            var item = Definition(pickup);
            int group = Array.IndexOf(Names, Group(item));
            if (!item || group < 0) return int.MaxValue;
            int subTier = item.tier == ItemTier.VoidTier2 ? 1 : item.tier == ItemTier.VoidTier3 ? 2 : item.tier == ItemTier.VoidBoss ? 3 : 0;
            return group * 10 + subTier;
        }
        public static bool IsExclusivePickup(string pickup)
        {
            var item = Definition(pickup);
            return item && (item == RoR2Content.Items.TitanGoldDuringTP || item == DLC1Content.Items.VoidMegaCrabItem ||
                item == DLC3Content.Items.MasterCore || item == DLC3Content.Items.MasterBattery ||
                item == DLC3Content.Items.PowerCube || item == DLC3Content.Items.PowerPyramid);
        }
        public static bool TryVoidMarkerColor(ItemDef item, out Color color)
        {
            color = Color.white;
            if (!item) return false;
            ItemTier baseTier;
            switch (item.tier)
            {
                case ItemTier.VoidTier1: baseTier = ItemTier.Tier1; break;
                case ItemTier.VoidTier2: baseTier = ItemTier.Tier2; color = new Color(0.47f, 0.85f, 0.31f); break;
                case ItemTier.VoidTier3: baseTier = ItemTier.Tier3; color = new Color(0.95f, 0.25f, 0.25f); break;
                case ItemTier.VoidBoss: baseTier = ItemTier.Boss; color = new Color(1f, 0.85f, 0.25f); break;
                default: return false;
            }
            var definition = ItemTierCatalog.GetItemTierDef(baseTier);
            if (definition) color = ColorCatalog.GetColor(definition.colorIndex);
            return true;
        }
        public static IEnumerable<ItemDef> Available(string group)
        {
            if (!Run.instance) return Enumerable.Empty<ItemDef>();
            return ItemCatalog.allItemDefs.Where(item => !item.hidden && Group(item) == group && Run.instance.availableItems.Contains(item.itemIndex))
                .OrderBy(item => (int)item.itemIndex);
        }
    }
}
