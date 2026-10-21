using System;
using System.Collections.Generic;
using System.Reflection;
using CommandQueueRemastered.Core;
using HarmonyLib;
using RoR2;

namespace CommandQueueRemastered.Game
{
    internal static class DistributionSourceContext
    {
        internal static DropSource Current;
        internal static CharacterMaster SkillUser;
        internal struct Snapshot { internal DropSource Source; internal CharacterMaster User; }
    }
    [HarmonyPatch]
    internal static class DistributionSourcePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ChestBehavior), "BaseItemDrop");
            yield return AccessTools.Method(typeof(OptionChestBehavior), "ItemDrop");
            yield return AccessTools.Method(typeof(ShopTerminalBehavior), "DropPickup", new[] { typeof(bool) });
            yield return AccessTools.Method(typeof(BossGroup), "DropRewards");
            yield return AccessTools.Method(typeof(EntityStates.HallowedCauldron.GrantHexes), "SpawnHex");
            yield return AccessTools.Method(typeof(EntityStates.HallowedCauldron.DrainingItems), "CreatePotentialServer");
            yield return AccessTools.Method(typeof(EntityStates.HallowedCauldron.DrainingItems), "TakeItemServer");
            yield return AccessTools.Method(typeof(EntityStates.Drifter.Salvage), "DropTempItemServer");
        }
        private static void Prefix(object __instance, out DistributionSourceContext.Snapshot __state)
        {
            __state = new DistributionSourceContext.Snapshot { Source = DistributionSourceContext.Current, User = DistributionSourceContext.SkillUser };
            DistributionSourceContext.SkillUser = null;
            if (__instance is EntityStates.Drifter.Salvage salvage)
            {
                DistributionSourceContext.Current = DropSource.DrifterSalvage;
                var body = salvage.outer ? salvage.outer.GetComponent<CharacterBody>() : null;
                DistributionSourceContext.SkillUser = body ? body.master : null;
            }
            else if (__instance is ChestBehavior || __instance is OptionChestBehavior) DistributionSourceContext.Current = DropSource.Chest;
            else if (__instance is ShopTerminalBehavior terminal)
            {
                var purchase = terminal.GetComponent<PurchaseInteraction>();
                if (purchase && purchase.costType == CostTypeIndex.LunarItemOrEquipment ||
                    terminal.name.IndexOf("Cauldron", StringComparison.OrdinalIgnoreCase) >= 0)
                    DistributionSourceContext.Current = DropSource.ExchangePool;
                else if (purchase && purchase.costType == CostTypeIndex.Money)
                    DistributionSourceContext.Current = DropSource.Chest;
            }
            else if (__instance is BossGroup boss)
            {
                if (TeleporterInteraction.instance && TeleporterInteraction.instance.bossGroup == boss)
                    DistributionSourceContext.Current = DropSource.Teleporter;
            }
            else DistributionSourceContext.Current = DropSource.ExchangePool;
        }
        private static Exception Finalizer(DistributionSourceContext.Snapshot __state, Exception __exception)
        { DistributionSourceContext.Current = __state.Source; DistributionSourceContext.SkillUser = __state.User; return __exception; }
    }
}
