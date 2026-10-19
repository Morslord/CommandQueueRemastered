using System;
using HarmonyLib;
using RoR2;
using RoR2.Artifacts;
using CommandQueueRemastered.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace CommandQueueRemastered.Game
{
    internal sealed class DeferredEssenceDrop
    {
        internal readonly GenericPickupController.CreatePickupInfo Info;
        internal readonly Vector3 OriginalPosition, OriginalVelocity;
        internal readonly CharacterMaster Recipient;
        internal PickupDropletController CreatedDroplet;
        internal DeferredEssenceDrop(GenericPickupController.CreatePickupInfo info, Vector3 position,
            Vector3 velocity, CharacterMaster recipient)
        {
            // Native creation would capture the active artifact at the original call time.
            if (CommandArtifactManager.IsCommandArtifactEnabled)
                info.artifactFlag |= GenericPickupController.PickupArtifactFlag.Command;
            if (info.pickerOptions != null)
                info.pickerOptions = (PickupPickerController.Option[])info.pickerOptions.Clone();
            Info = info; OriginalPosition = position; OriginalVelocity = velocity; Recipient = recipient;
        }
    }

    internal static class FastAssignedEssence
    {
        internal const int MaximumDropsPerSecond = 30;
        private static readonly DeferredWorkQueue<DeferredEssenceDrop> pending =
            new DeferredWorkQueue<DeferredEssenceDrop>(1.0 / MaximumDropsPerSecond);
        private static DeferredEssenceDrop creating;
        private static bool replayNext;
        private static float retryAfter;
        private static int queued, created, initialized;
        internal static void Enqueue(GenericPickupController.CreatePickupInfo info, Vector3 position,
            Vector3 velocity, CharacterMaster recipient)
        {
            pending.Add(new DeferredEssenceDrop(info, position, velocity, recipient)); queued++;
        }
        internal static bool BeginReplay()
        {
            if (!replayNext) return false;
            replayNext = false; return true;
        }
        internal static void Capture(PickupDropletController droplet)
        {
            if (creating != null && !replayNext && !creating.CreatedDroplet) creating.CreatedDroplet = droplet;
        }
        internal static void Process()
        {
            if (!NetworkServer.active || !Run.instance || Time.timeScale <= 0 || Time.unscaledTime < retryAfter ||
                !pending.TryBegin(Time.frameCount, Time.unscaledTimeAsDouble, out var request)) return;
            bool completed = false;
            try
            {
                var recipient = request.Recipient ? request.Recipient.GetBody() : null;
                bool assigned = recipient && recipient.healthComponent && recipient.healthComponent.alive;
                var position = assigned ? recipient.corePosition : request.OriginalPosition;
                var info = request.Info; info.position = position;
                creating = request; replayNext = true;
                // No waiting GameObject: instantiate and network-spawn only when this request is due.
                PickupDropletController.CreatePickupDroplet(info, position, assigned ? Vector3.zero : request.OriginalVelocity);
                pending.Complete(); completed = true; created++;
                if (assigned && request.CreatedDroplet)
                {
                    // Native Start only loads presentation; state/info are set before creation returns.
                    request.CreatedDroplet.ForcePickupInitialization(); initialized++;
                }
            }
            catch (Exception exception)
            {
                if (!completed)
                {
                    // Existing native droplets retain normal physics; replaying them could duplicate items.
                    if (request.CreatedDroplet) { pending.Complete(); created++; }
                    else { pending.Retry(); retryAfter = Time.unscaledTime + 5f; }
                }
                RemasteredPlugin.Instance?.LogError("Deferred essence delivery failed; uncreated drop data is retained", exception);
            }
            finally
            {
                creating = null; replayNext = false;
                if (pending.Count == 0)
                {
                    if (queued > 1) RemasteredPlugin.Instance?.LogInfo("Assigned essence delivery: queued=" + queued +
                        "; nativeCalls=" + created + "; fastInitializationCalls=" + initialized + "; pending=0.");
                    queued = created = initialized = 0;
                }
            }
        }
        internal static void Reset()
        {
            if (pending.Count > 0) RemasteredPlugin.Instance?.LogInfo("Assigned essence delivery reset: pending=" + pending.Count + ".");
            pending.Clear(); creating = null; replayNext = false; retryAfter = 0;
            queued = created = initialized = 0;
        }
    }

    [HarmonyPatch(typeof(PickupDropletController), "OnEnable")]
    internal static class AssignedEssenceCapturePatch
    {
        private static void Postfix(PickupDropletController __instance)
        { if (NetworkServer.active) FastAssignedEssence.Capture(__instance); }
    }
}
