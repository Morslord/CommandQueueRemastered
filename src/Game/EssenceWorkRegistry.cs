using System;
using System.Collections.Generic;
using HarmonyLib;
using RoR2;
using CommandQueueRemastered.Core;
using UnityEngine;

namespace CommandQueueRemastered.Game
{
    internal sealed class EssenceCandidate
    {
        internal readonly PickupPickerController Picker;
        internal readonly Collider Collider;
        internal float RetryAt;
        internal EssenceCandidate(PickupPickerController picker)
        {
            Picker = picker; Collider = picker.GetComponent<Collider>();
        }
    }
    internal static class EssenceWorkRegistry
    {
        internal const int DistanceBudget = 32;
        internal const int ChoiceBudget = 4;
        internal static readonly BoundedWorkQueue<EssenceCandidate> Candidates = new BoundedWorkQueue<EssenceCandidate>();
        private static readonly Dictionary<PickupPickerController, EssenceCandidate> entries = new Dictionary<PickupPickerController, EssenceCandidate>();
        private static readonly Dictionary<LocalUser, NetworkUIPromptController> activePrompts = new Dictionary<LocalUser, NetworkUIPromptController>();
        private static readonly Dictionary<LocalUser, float> pendingInteractions = new Dictionary<LocalUser, float>();
        private static readonly HashSet<NetworkUIPromptController> prompts = new HashSet<NetworkUIPromptController>();
        internal static void TrackPrompt(NetworkUIPromptController prompt)
        {
            if (!prompts.Add(prompt)) return;
            prompt.onDisplayBegin += Begin; prompt.onDisplayEnd += End;
        }
        internal static void UntrackPrompt(NetworkUIPromptController prompt)
        {
            prompts.Remove(prompt);
            prompt.onDisplayBegin -= Begin; prompt.onDisplayEnd -= End;
            foreach (var user in LocalUserManager.readOnlyLocalUsersList)
                if (activePrompts.TryGetValue(user, out var active) && active == prompt) activePrompts.Remove(user);
        }
        internal static void Register(PickupPickerController picker)
        {
            if (entries.ContainsKey(picker)) return;
            var candidate = new EssenceCandidate(picker);
            entries.Add(picker, candidate); Candidates.Add(candidate, prioritize: true);
        }
        internal static void Unregister(PickupPickerController picker)
        {
            if (!entries.TryGetValue(picker, out var candidate)) return;
            entries.Remove(picker); Candidates.Remove(candidate);
        }
        internal static void Begin(NetworkUIPromptController prompt, LocalUser user, CameraRigController camera)
        { if (user != null) { activePrompts[user] = prompt; pendingInteractions.Remove(user); } }
        internal static void End(NetworkUIPromptController prompt, LocalUser user, CameraRigController camera)
        {
            if (user != null && activePrompts.TryGetValue(user, out var active) && active == prompt) activePrompts.Remove(user);
        }
        internal static bool Busy(LocalUser user)
        {
            if (activePrompts.TryGetValue(user, out var prompt))
            {
                if (prompt && prompt.isActiveAndEnabled && prompt.inUse && RemasteredPlugin.InteractionActive(prompt, user)) return true;
                activePrompts.Remove(user);
            }
            return pendingInteractions.TryGetValue(user, out float until) && Time.unscaledTime < until;
        }
        internal static void Request(LocalUser user) => pendingInteractions[user] = Time.unscaledTime + 1f;
        internal static void ResetInteractions() { activePrompts.Clear(); pendingInteractions.Clear(); }
        internal static void Clear()
        {
            foreach (var prompt in prompts)
                if (prompt) { prompt.onDisplayBegin -= Begin; prompt.onDisplayEnd -= End; }
            prompts.Clear(); ResetInteractions(); entries.Clear(); Candidates.Clear();
        }
    }

    [HarmonyPatch(typeof(PickupPickerController), "OnEnable")]
    internal static class EssenceRegisterPatch
    { private static void Postfix(PickupPickerController __instance) => EssenceWorkRegistry.Register(__instance); }
    [HarmonyPatch(typeof(PickupPickerController), "OnDisable")]
    internal static class EssenceUnregisterPatch
    { private static void Prefix(PickupPickerController __instance) => EssenceWorkRegistry.Unregister(__instance); }
    [HarmonyPatch(typeof(NetworkUIPromptController), "OnEnable")]
    internal static class EssencePromptRegisterPatch
    {
        private static void Postfix(NetworkUIPromptController __instance)
        {
            EssenceWorkRegistry.TrackPrompt(__instance);
        }
    }
    [HarmonyPatch(typeof(NetworkUIPromptController), "OnDisable")]
    internal static class EssencePromptUnregisterPatch
    {
        private static void Prefix(NetworkUIPromptController __instance)
        {
            EssenceWorkRegistry.UntrackPrompt(__instance);
        }
    }
}
