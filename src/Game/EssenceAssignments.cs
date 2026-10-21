using System;
using System.Collections.Generic;
using System.Reflection;
using CommandQueueRemastered.Core;
using HarmonyLib;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace CommandQueueRemastered.Game
{
    internal sealed class EssenceAssignmentTag : MonoBehaviour
    {
        internal DistributionReservation Token;
        private void OnDestroy() { EssenceAssignments.ReleaseOwned(Token, gameObject); }
    }

    internal static class EssenceAssignments
    {
        internal static readonly DistributionLedger Ledger = new DistributionLedger();
        internal static DistributionReservation Current;
        private static readonly Dictionary<DistributionReservation, object> owners = new Dictionary<DistributionReservation, object>();
        private static readonly Dictionary<ItemTransferOrb, DistributionReservation> orbs = new Dictionary<ItemTransferOrb, DistributionReservation>();
        internal static void Attach(GameObject target, DistributionReservation token)
        {
            if (!target || token == null) return;
            var tag = target.GetComponent<EssenceAssignmentTag>() ?? target.AddComponent<EssenceAssignmentTag>();
            tag.Token = token; owners[token] = target;
        }
        internal static void Release(DistributionReservation token)
        { Ledger.Release(token); if (token != null) owners.Remove(token); }
        internal static void ReleaseOwned(DistributionReservation token, object owner)
        { if (token != null && owners.TryGetValue(token, out var current) && ReferenceEquals(current, owner)) Release(token); }
        internal static DistributionReservation Token(Component component) => component ? component.GetComponent<EssenceAssignmentTag>()?.Token : null;
        internal static void CaptureOrb(ItemTransferOrb orb)
        {
            if (Current == null || orb == null) return;
            if (orb.stack <= 0) { Release(Current); return; }
            var master = orb.inventoryToGrantTo ? orb.inventoryToGrantTo.GetComponent<CharacterMaster>() : null;
            if (master) Current.Recipient = master.netId.Value;
            owners[Current] = orb; orbs[orb] = Current;
        }
        internal static void Arrived(ItemTransferOrb orb)
        { if (orbs.TryGetValue(orb, out var token)) { Release(token); orbs.Remove(orb); } }
        internal static void CancelOrbs()
        { foreach (var token in orbs.Values) Release(token); orbs.Clear(); }
        internal static void Reset() { Current = null; Ledger.Reset(); owners.Clear(); orbs.Clear(); }
    }

    // Native droplet -> cube -> selected pickup/orb retains one reservation throughout.
    [HarmonyPatch]
    internal static class AssignmentScopePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PickupDropletController), "CreatePickup");
            yield return AccessTools.Method(typeof(PickupPickerController), "HandlePickupSelected", new[] { typeof(int) });
        }
        private static void Prefix(Component __instance, out DistributionReservation __state)
        { __state = EssenceAssignments.Current; EssenceAssignments.Current = EssenceAssignments.Token(__instance); }
        private static Exception Finalizer(DistributionReservation __state, Exception __exception)
        { EssenceAssignments.Current = __state; return __exception; }
    }
    [HarmonyPatch(typeof(PickupPickerController), "OnEnable")]
    internal static class AssignmentCubePatch
    {
        private static void Postfix(PickupPickerController __instance)
        { if (NetworkServer.active) EssenceAssignments.Attach(__instance.gameObject, EssenceAssignments.Current); }
    }
    [HarmonyPatch(typeof(GenericPickupController), "CreatePickup")]
    internal static class AssignmentPickupPatch
    {
        private static void Postfix(GenericPickupController __result)
        {
            if (!NetworkServer.active || !__result) return;
            if (__result.pickup.isTempItem || __result.pickup.isRental) EssenceAssignments.Release(EssenceAssignments.Current);
            else EssenceAssignments.Attach(__result.gameObject, EssenceAssignments.Current);
        }
    }
    [HarmonyPatch(typeof(GenericPickupController), "AttemptGrant")]
    internal static class AssignmentConsumedPatch
    {
        private static void Postfix(GenericPickupController __instance, bool ___consumed)
        { if (NetworkServer.active && ___consumed) EssenceAssignments.Release(EssenceAssignments.Token(__instance)); }
    }
    [HarmonyPatch(typeof(ItemTransferOrb), "DispatchItemTransferOrb", new[] { typeof(ItemTransferOrb.DispatchArgs) })]
    internal static class AssignmentOrbPatch
    {
        private static void Postfix(ItemTransferOrb __result) { EssenceAssignments.CaptureOrb(__result); }
    }
    [HarmonyPatch(typeof(ItemTransferOrb), "OnArrival")]
    internal static class AssignmentArrivalPatch
    {
        private static Exception Finalizer(ItemTransferOrb __instance, Exception __exception)
        { EssenceAssignments.Arrived(__instance); return __exception; }
    }
    [HarmonyPatch(typeof(OrbManager), "OnDisable")]
    internal static class AssignmentOrbCleanupPatch
    {
        private static void Prefix() { EssenceAssignments.CancelOrbs(); }
    }
}
