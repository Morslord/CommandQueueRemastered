using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RoR2;
using RoR2.Artifacts;
using CommandQueueRemastered.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace CommandQueueRemastered.Game
{
    // Only the server changes world drops. This never gives ownership of an essence.
    internal static class EssenceDrops
    {
        internal static readonly EssenceDistribution Distribution = new EssenceDistribution();
        internal static CharacterMaster Killer;
        internal static CharacterMaster PlayerOwner(CharacterMaster master)
        {
            var visited = new HashSet<CharacterMaster>();
            while (master && visited.Add(master))
            {
                if (master.playerCharacterMasterController) return master;
                var ownership = master.GetComponent<MinionOwnership>();
                master = ownership ? ownership.ownerMaster : null;
            }
            return null;
        }
        internal static CharacterMaster Relocate(ref GenericPickupController.CreatePickupInfo info, ref Vector3 position, ref Vector3 velocity)
        {
            var plugin = RemasteredPlugin.Instance;
            if (!NetworkServer.active || !plugin || !Run.instance || info.pickup.ignoreArtifactBasedPickupReplacements ||
                (!CommandArtifactManager.IsCommandArtifactEnabled && (info.artifactFlag & GenericPickupController.PickupArtifactFlag.Command) == 0) ||
                (info.artifactFlag & GenericPickupController.PickupArtifactFlag.Delusion) != 0) return null;
            var pickup = PickupCatalog.GetPickupDef(info.pickup.pickupIndex);
            var item = pickup == null ? null : ItemCatalog.GetItemDef(pickup.itemIndex);
            // Equipment and unrelated pickups retain their original location.
            if (pickup == null || pickup.equipmentIndex != EquipmentIndex.None) return null;
            ItemTier tier = item ? item.tier : pickup.itemTier;
            if (!plugin.EssenceDropModes.TryGetValue(tier, out var setting) || setting.Value == EssenceDropMode.Original) return null;
            var players = PlayerCharacterMasterController.instances.Select(player => player.master)
                .Where(master => master && master.inventory && master.GetBody() && master.GetBody().healthComponent &&
                    master.GetBody().healthComponent.alive && master.teamIndex == TeamIndex.Player)
                .Distinct().ToArray();
            long Count(CharacterMaster master)
            {
                long count = 0;
                foreach (var definition in ItemCatalog.allItemDefs)
                    if (plugin.FewestItemsCount.Value == InventoryCountMode.AllItems || definition.tier == tier)
                        count += master.inventory.GetItemCountPermanent(definition.itemIndex);
                return count;
            }
            long? recipient = Distribution.Choose(tier.ToString(), setting.Value,
                players.Select(master => new DropCandidate(master.netId.Value, setting.Value == EssenceDropMode.FewestItems ? Count(master) : 0,
                    plugin.ExcludePausedPlayers.Value && plugin.PlayerPaused(master, ItemGroups.Group(tier)))),
                Killer ? (long?)Killer.netId.Value : null, plugin.ExcludePausedPlayers.Value);
            var target = players.FirstOrDefault(master => master.netId.Value == recipient);
            var body = target ? target.GetBody() : null;
            if (!body) return null;
            position = body.corePosition;
            info.position = position;
            velocity = Vector3.zero;
            return target;
        }
    }

    [HarmonyPatch]
    internal static class DeathDropContextPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GlobalEventManager), "OnCharacterDeath", new[] { typeof(DamageReport) });
            yield return AccessTools.Method(typeof(GlobalEventManager), "HandleCharacterDeath", new[] { typeof(DamageReport) });
            yield return AccessTools.Method(typeof(DeathRewards), "OnKilledServer", new[] { typeof(DamageReport) });
            yield return AccessTools.Method(typeof(GlobalDeathRewards), "OnCharacterDeathGlobal", new[] { typeof(DamageReport) });
        }
        private static void Prefix(DamageReport __0, out CharacterMaster __state)
        {
            __state = EssenceDrops.Killer;
            EssenceDrops.Killer = EssenceDrops.PlayerOwner(__0?.attackerMaster);
        }
        private static Exception Finalizer(Exception __exception, CharacterMaster __state)
        { EssenceDrops.Killer = __state; return __exception; }
    }

    [HarmonyPatch(typeof(PickupDropletController), "CreatePickupDroplet",
        new[] { typeof(GenericPickupController.CreatePickupInfo), typeof(Vector3), typeof(Vector3) })]
    internal static class EssenceLocationPatch
    {
        private static bool Prefix(ref GenericPickupController.CreatePickupInfo __0, ref Vector3 __1, ref Vector3 __2)
        {
            if (FastAssignedEssence.BeginReplay()) return true;
            var info = __0; var position = __1; var velocity = __2;
            try
            {
                var target = EssenceDrops.Relocate(ref info, ref position, ref velocity);
                if (!target) return true;
                FastAssignedEssence.Enqueue(__0, __1, __2, target);
                return false;
            }
            catch (Exception exception)
            { RemasteredPlugin.Instance?.LogError("Essence relocation failed; keeping normal drop", exception); return true; }
        }
    }
}
