using System;
using System.Collections.Generic;
using HarmonyLib;
using CommandQueueRemastered.Core;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace CommandQueueRemastered.Game
{
    internal sealed class QueuedCollectionRequest : MonoBehaviour
    {
        internal QueuedCollectionTicket Ticket;
    }

    internal static class AutomaticCollection
    {
        // Native picker messages 0/1 remain unchanged. Vanilla ignores unknown message types.
        internal const byte Message = 213;
        internal const uint Signature = 0x43515235;
        internal static readonly HashSet<PickupPickerController> Selecting = new HashSet<PickupPickerController>();
        internal static bool IsCommand(PickupPickerController picker)
        {
            var cube = RoR2.Artifacts.CommandArtifactManager.commandCubePrefab;
            var original = cube ? cube.GetComponent<PickupPickerController>() : null;
            return picker && original && picker.panelPrefab == original.panelPrefab && !picker.isDelusionChoice && !picker.isRebirthChoice;
        }
        internal static void Submit(PickupPickerController picker, NetworkUIPromptController prompt, int option, bool queued)
        {
            bool collect = queued && RemasteredPlugin.Instance.AutoCollectQueuedItems.Value;
            if (collect && !NetworkServer.active)
            {
                // The native prompt validates the sender against its current participant before dispatch.
                // Send a marker on the same reliable channel, followed by the normal selection.
                var writer = prompt.BeginMessageToServer();
                writer.Write(Message);
                writer.Write(Signature);
                writer.Write(option);
                prompt.FinishMessageToServer(writer);
            }
            if (collect && NetworkServer.active) Selecting.Add(picker);
            try { picker.SubmitChoice(option); }
            finally { Selecting.Remove(picker); }
        }
        internal static bool Receive(PickupPickerController picker, NetworkReader reader)
        {
            uint start = reader.Position;
            byte message;
            try { message = reader.ReadByte(); }
            catch { return false; }
            if (message != Message)
            {
                if (message == 1)
                {
                    var cancelled = picker.GetComponent<QueuedCollectionRequest>();
                    if (cancelled) cancelled.Ticket = null;
                }
                // Leave the native reader exactly where it was for submit/cancel or foreign messages.
                reader.SeekZero();
                reader.ReadBytes((int)start);
                return true;
            }
            try
            {
                if (reader.ReadUInt32() != Signature) return false;
                int option = reader.ReadInt32();
                var plugin = RemasteredPlugin.Instance;
                var prompt = picker.GetComponent<NetworkUIPromptController>();
                if (!NetworkServer.active || !plugin || !plugin.AutoCollectQueuedItems.Value || !IsCommand(picker) ||
                    !prompt || !prompt.currentParticipantMaster || picker.options == null || option < 0 || option >= picker.options.Length || !picker.options[option].available) return false;
                var request = picker.GetComponent<QueuedCollectionRequest>() ?? picker.gameObject.AddComponent<QueuedCollectionRequest>();
                request.Ticket = new QueuedCollectionTicket(prompt.currentParticipantMaster.netId.Value, option, Time.unscaledTime + 2f);
            }
            catch (Exception exception) { RemasteredPlugin.Instance?.LogError("Invalid queued collection marker ignored", exception); }
            return false;
        }
    }

    [HarmonyPatch(typeof(PickupPickerController), "HandleClientMessage")]
    internal static class QueuedCollectionMessagePatch
    {
        private static bool Prefix(PickupPickerController __instance, NetworkReader __0) => AutomaticCollection.Receive(__instance, __0);
    }

    [HarmonyPatch(typeof(PickupPickerController), "HandlePickupSelected")]
    internal static class QueuedCollectionSelectionPatch
    {
        private static void Prefix(PickupPickerController __instance, int __0, out bool __state)
        {
            __state = false;
            var request = __instance.GetComponent<QueuedCollectionRequest>();
            if (!request) return;
            var ticket = request.Ticket;
            request.Ticket = null; // Every marker is single use, including invalid/stale requests.
            var prompt = __instance.GetComponent<NetworkUIPromptController>();
            if (NetworkServer.active && RemasteredPlugin.Instance && RemasteredPlugin.Instance.AutoCollectQueuedItems.Value &&
                ticket != null && prompt && prompt.currentParticipantMaster &&
                ticket.TryConsume(prompt.currentParticipantMaster.netId.Value, __0, Time.unscaledTime))
                __state = AutomaticCollection.Selecting.Add(__instance);
        }
        private static Exception Finalizer(PickupPickerController __instance, bool __state, Exception __exception)
        {
            if (__state) AutomaticCollection.Selecting.Remove(__instance);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(PickupPickerController), "CreatePickup", new[] { typeof(UniquePickup) })]
    internal static class QueuedCollectionGrantPatch
    {
        private static bool Prefix(PickupPickerController __instance, UniquePickup __0)
        {
            if (!NetworkServer.active || !AutomaticCollection.Selecting.Remove(__instance) || !AutomaticCollection.IsCommand(__instance)) return true;
            var definition = PickupCatalog.GetPickupDef(__0.pickupIndex);
            var prompt = __instance.GetComponent<NetworkUIPromptController>();
            var body = prompt && prompt.currentParticipantMaster ? prompt.currentParticipantMaster.GetBody() : null;
            if (definition == null || definition.itemIndex == ItemIndex.None || !body || !body.inventory || !body.healthComponent || !body.healthComponent.alive) return true;
            // Native item-transfer orbs preserve permanent/temporary/rental state and pickup feedback.
            // Scope is already consumed, so the native method's CreatePickup fallback cannot recurse.
            __instance.AttemptGrantPickupDirectly(__0);
            return false;
        }
    }
}
