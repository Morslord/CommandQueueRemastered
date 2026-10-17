using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using RoR2;
using RoR2.Artifacts;
using CommandQueueRemastered.Core;
using CommandQueueRemastered.Game;
using CommandQueueRemastered.Integrations;
using UnityEngine;

namespace CommandQueueRemastered
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.KingEnderBrine.ProperSave", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("com.kuberoot.commandqueuewithsave")]
    [BepInIncompatibility("local.commandqueue.iconfix")]
    [BepInIncompatibility("ProperSave_CommandQueueExtended")]
    public sealed class RemasteredPlugin : BaseUnityPlugin
    {
        public const string Guid = "local.commandqueue.remastered";
        public const string Name = "CommandQueue Remastered";
        public const string Version = "0.3.3";
        internal const float ItemIconBaseSize = 51.2f;
        internal static RemasteredPlugin Instance;
        internal readonly QueueState Queue = new QueueState(ItemGroups.GroupOf);
        internal PresetStore Presets;
        internal readonly Dictionary<string, ConfigEntry<bool>> GroupOptions = new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        internal ConfigEntry<bool> RotateRepeatedItems;
        internal ConfigEntry<float> ItemScale, MenuScale;
        internal ConfigEntry<SpecialPickupBehavior> TemporaryItemBehavior, EtherealItemBehavior;
        internal ConfigEntry<ExclusiveItemBehavior> ExclusiveItemSelection;
        internal Sprite ModIcon;
        internal Texture2D ModIconTexture;
        private Harmony harmony;
        private ProperSaveAdapter properSave;
        private readonly HashSet<PickupPickerController> pendingSelections = new HashSet<PickupPickerController>();
        private readonly HashSet<PickupPickerController> manualDisplays = new HashSet<PickupPickerController>();
        private static readonly System.Reflection.MethodInfo localParticipantGetter =
            AccessTools.PropertyGetter(typeof(NetworkUIPromptController), "currentLocalParticipant");
        private static bool InteractionActive(NetworkUIPromptController prompt, LocalUser user) =>
            prompt && user != null && ReferenceEquals(localParticipantGetter.Invoke(prompt, null), user);
        private readonly List<ScoreboardQueueUI> scoreboardViews = new List<ScoreboardQueueUI>();
        private float nextScoreboardScan;

        private void Awake()
        {
            Instance = this;
            // Prevent two queue handlers consuming the same pickup. Never edit the user's profile automatically.
            string[] incompatible = { "com.kuberoot.commandqueuewithsave", "local.commandqueue.iconfix", "ProperSave_CommandQueueExtended" };
            if (incompatible.Any(Chainloader.PluginInfos.ContainsKey))
            {
                Logger.LogError("Disable CommandQueueExtended, IconFix and ProperSave_CommandQueueExtended before using Remastered. Remastered initialization stopped to avoid conflicting queue handlers.");
                enabled = false;
                return;
            }
            try
            {
                foreach (string group in ItemGroups.Names)
                    GroupOptions.Add(group, Config.Bind("Groups", group, true, "Enable the " + group + " queue group."));
                RotateRepeatedItems = Config.Bind("General", "RotateRepeatedItems", true, "Move collected repeated items to the queue end. When disabled, keep displayed order and quantities while cycling through every queued item internally.");
                Queue.RotateRepeatedItems = RotateRepeatedItems.Value;
                RotateRepeatedItems.SettingChanged += (_, __) => Queue.RotateRepeatedItems = RotateRepeatedItems.Value;
                const string specialDescription = "ManualMenu: open the normal picker. UseQueueWithoutConsuming: select the tier's current queued item without changing quantities, order or Repeat progress. UseQueueNormally: select and consume normally. Missing or unavailable queued items open the normal picker.";
                TemporaryItemBehavior = Config.Bind("Special items", "TemporaryItemBehavior", SpecialPickupBehavior.ManualMenu, "Behavior for timed temporary Command pickups. " + specialDescription);
                EtherealItemBehavior = Config.Bind("Special items", "EtherealItemBehavior", SpecialPickupBehavior.ManualMenu, "Behavior for ethereal Command pickups. " + specialDescription);
                ExclusiveItemSelection = Config.Bind("Special items", "ExclusiveItemSelection", ExclusiveItemBehavior.Default,
                    "Halcyon Seed, Newly Hatched Zoea, Exposed Cerebellum, Encrypted Cerebellum, Prison Matrix and Sentry Key: Default selects from their exclusive Command essence when queued at any position. Automatic also selects without a queue entry. Existing entries follow normal consumption and Repeat rules. Pause and temporary/ethereal policies take priority. No automatic collection.");
                MenuScale = Config.Bind("General", "MenuScale", 1f,
                    new ConfigDescription("Queue menu scale; capped to fit the screen. Default: 1.0x.", new AcceptableValueRange<float>(0.5f, 2f)));
                ItemScale = Config.Bind("General", "ItemScale", 1f, new ConfigDescription("Item button scale. 1.0x uses the compact 51.2-unit base size.", new AcceptableValueRange<float>(0.5f, 2f)));
                Presets = new PresetStore(System.IO.Path.Combine(Paths.ConfigPath, "commandQueueRemastered.presets.json"));
                try { Presets.Load(); }
                catch (Exception exception) { Logger.LogError("Preset file could not be loaded; existing file retained: " + exception); presetsReadFailed = true; }
                if (!presetsReadFailed)
                {
                    try
                    {
                        int imported = Presets.ImportLegacy(System.IO.Path.Combine(Paths.ConfigPath, "commandQueueSaves.json"));
                        if (imported > 0) Logger.LogInfo("Imported " + imported + " legacy CommandQueue presets. Source file retained unchanged.");
                    }
                    catch (Exception exception) { Logger.LogError("Legacy preset import failed; no presets imported. Import will retry on next startup: " + exception); }
                }
                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(RemasteredPlugin).Assembly);
                LookingGlassCommandCompatibility.Register(harmony, this);
                Run.onRunStartGlobal += OnRunStart;
                Run.onRunDestroyGlobal += OnRunEnd;
                OptionalOptions.Register(this);
                properSave = new ProperSaveAdapter(this);
                properSave.Initialize();
                Logger.LogInfo(Name + " " + Version + " initialized. Interface: game scoreboard. Game: " + Application.version + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError("Remastered initialization failed: " + exception);
                OnDestroy();
                enabled = false;
            }
        }
        internal bool presetsReadFailed;
        internal void LogError(string context, Exception exception) => Logger.LogError(context + ": " + exception);
        internal void LogInfo(string message) => Logger.LogInfo(message);
        internal void LogWarning(string message) => Logger.LogWarning(message);
        internal bool HasPendingSelection(PickupPickerController picker) => pendingSelections.Contains(picker);
        private SpecialPickupBehavior SelectionBehavior(UniquePickup pickup) => SpecialPickupPolicy.Resolve(
            pickup.isTempItem, pickup.isRental, TemporaryItemBehavior.Value, EtherealItemBehavior.Value);
        internal bool GroupEnabled(string group) => group != null && GroupOptions.TryGetValue(group, out var option) && option.Value;
        internal bool BlocksGameplay(LocalUser user) => scoreboardViews.Any(view => view && view.BlocksGameplay(user));
        internal bool SuppressPauseShortcut => scoreboardViews.Any(view => view && view.SuppressPauseShortcut);

        private void Update()
        {
            AttachScoreboards();
            properSave?.Update();
        }
        private void AttachScoreboards()
        {
            if (Time.unscaledTime < nextScoreboardScan) return;
            nextScoreboardScan = Time.unscaledTime + 0.5f;
            scoreboardViews.RemoveAll(view => !view);
            foreach (var hud in UnityEngine.Object.FindObjectsOfType<RoR2.UI.HUD>())
            {
                if (!hud.scoreboardPanel) continue;
                var controller = hud.scoreboardPanel.GetComponentInChildren<RoR2.UI.ScoreboardController>(true);
                if (!controller || controller.GetComponent<ScoreboardQueueUI>()) continue;
                var view = controller.gameObject.AddComponent<ScoreboardQueueUI>();
                try { view.Initialize(this, hud, controller); scoreboardViews.Add(view); }
                catch (Exception exception) { view.DisableAfterFailure(); LogError("Scoreboard initialization failed", exception); }
            }
        }
        private void OnRunStart(Run run) { Queue.Reset(); pendingSelections.Clear(); }
        private void OnRunEnd(Run run) { Queue.Reset(); pendingSelections.Clear(); }
        private void OnDestroy()
        {
            foreach (var view in scoreboardViews) if (view) Destroy(view);
            scoreboardViews.Clear();
            properSave?.Dispose();
            Run.onRunStartGlobal -= OnRunStart;
            Run.onRunDestroyGlobal -= OnRunEnd;
            harmony?.UnpatchSelf();
            if (ModIcon) Destroy(ModIcon);
            if (ModIconTexture) Destroy(ModIconTexture);
            if (Instance == this) Instance = null;
        }

        internal bool ScheduleSelection(PickupPickerController picker, LocalUser user,
            NetworkUIPromptController prompt, CameraRigController camera)
        {
            if (!picker || manualDisplays.Contains(picker)) return false;
            if (pendingSelections.Contains(picker)) return true;
            if (user == null || !Run.instance || picker.options == null || !prompt) return false;
            var cube = CommandArtifactManager.commandCubePrefab;
            var prefabPicker = cube ? cube.GetComponent<PickupPickerController>() : null;
            // Do not select from printers, scrappers, rebirth, delusion or unrelated choice windows.
            if (!prefabPicker || picker.panelPrefab != prefabPicker.panelPrefab || picker.isDelusionChoice || picker.isRebirthChoice) return false;
            var options = SelectionOptions(picker);
            string sourceTier = SourceTier(picker);
            int optionIndex = FindSelection(options, sourceTier);
            if (options.Any(option => ItemGroups.GroupOfTier(option.Tier) == "Void"))
                LogInfo("Void command essence: source tier=" + (sourceTier ?? "not received") +
                    "; option tiers=" + string.Join(",", options.Select(option => option.Tier).Where(tier => tier != null).Distinct()) +
                    "; queue choice=" + (optionIndex < 0 ? "manual menu" : options[optionIndex].Pickup + " (" + options[optionIndex].Tier + ")") + ".");
            if (optionIndex < 0) return false;
            var selected = options[optionIndex];
            bool exclusive = CommandSelection.IsExclusiveEssence(options, ItemGroups.IsExclusivePickup);
            int head = SelectionHead(options, selected.Tier);
            var entry = head < 0 ? null : Queue.Entries[head];
            var behavior = SelectionBehavior(picker.options[optionIndex].pickup);
            pendingSelections.Add(picker);
            try { StartCoroutine(SubmitNextFrame(picker, optionIndex, selected.Pickup, selected.Tier, user, prompt, camera, behavior, exclusive, entry)); }
            catch { pendingSelections.Remove(picker); throw; }
            return true;
        }

        private CommandOption[] SelectionOptions(PickupPickerController picker) => picker.options.Select(option =>
            {
                var pickup = PickupCatalog.GetPickupDef(option.pickup.pickupIndex);
                var item = pickup == null ? null : ItemCatalog.GetItemDef(pickup.itemIndex);
                return new CommandOption(item ? pickup.internalName : null, item ? item.tier.ToString() : null,
                    option.available && item && GroupEnabled(ItemGroups.Group(item)) &&
                    SelectionBehavior(option.pickup) != SpecialPickupBehavior.ManualMenu);
            }).ToArray();
        private static string SourceTier(PickupPickerController picker)
        {
            var source = PickupCatalog.GetPickupDef(picker.GetPickupIndex());
            var sourceItem = source == null ? null : ItemCatalog.GetItemDef(source.itemIndex);
            return sourceItem ? sourceItem.tier.ToString() : null;
        }
        private int FindSelection(CommandOption[] options, string sourceTier) => CommandSelection.FindOption(
            Queue, options, sourceTier, ItemGroups.TierOf, ItemGroups.IsExclusivePickup, ExclusiveItemSelection.Value);
        private int SelectionHead(CommandOption[] options, string tier, bool resetRepeatCycle = false) =>
            CommandSelection.FindQueueEntry(Queue, options, tier, ItemGroups.TierOf, ItemGroups.IsExclusivePickup, resetRepeatCycle);

        private IEnumerator SubmitNextFrame(PickupPickerController picker, int optionIndex, string pickup, string tier,
            LocalUser user, NetworkUIPromptController prompt, CameraRigController camera, SpecialPickupBehavior behavior,
            bool exclusive, QueueEntry plannedEntry)
        {
            yield return null;
            bool submitted = false;
            try
            {
                // A cancelled interaction must not submit a stale choice or reopen its panel.
                if (!picker || !InteractionActive(prompt, user)) yield break;
                if (Queue.IsPickupPaused(pickup) || picker.options == null || optionIndex >= picker.options.Length ||
                    !picker.options[optionIndex].available || PickupCatalog.GetPickupDef(picker.options[optionIndex].pickup.pickupIndex)?.internalName != pickup) yield break;
                var options = SelectionOptions(picker);
                if (FindSelection(options, SourceTier(picker)) != optionIndex ||
                    CommandSelection.IsExclusiveEssence(options, ItemGroups.IsExclusivePickup) != exclusive || options[optionIndex].Tier != tier) yield break;
                int head = SelectionHead(options, tier);
                if (!ReferenceEquals(plannedEntry, head < 0 ? null : Queue.Entries[head])) yield break;
                if (SelectionBehavior(picker.options[optionIndex].pickup) != behavior || !GroupEnabled(ItemGroups.GroupOf(pickup))) yield break;
                if (behavior == SpecialPickupBehavior.UseQueueNormally && head >= 0) head = SelectionHead(options, tier, true);
                picker.SubmitChoice(optionIndex);
                submitted = true;
                if (head >= 0) SpecialPickupPolicy.CompleteSelection(Queue, head, tier, behavior);
            }
            catch (Exception exception) { LogError("Automatic command selection failed; queue retained where possible", exception); }
            finally
            {
                pendingSelections.Remove(picker);
                if (!submitted && picker && InteractionActive(prompt, user) && camera)
                {
                    // Re-enter the game's display path once, bypassing automatic selection for this picker.
                    manualDisplays.Add(picker);
                    try { AccessTools.Method(typeof(PickupPickerController), "OnDisplayBegin")
                        .Invoke(picker, new object[] { prompt, user, camera }); }
                    catch (Exception exception) { LogError("Manual command picker fallback failed", exception); }
                    finally { manualDisplays.Remove(picker); }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PickupPickerController), "OnDisplayBegin")]
    internal static class CommandSelectionPatch
    {
        private static bool Prefix(PickupPickerController __instance, NetworkUIPromptController networkUIPromptController,
            LocalUser localUser, CameraRigController cameraRigController)
        {
            // Skip panel creation only when a validated queue choice has been scheduled.
            try { return !(RemasteredPlugin.Instance?.ScheduleSelection(__instance, localUser,
                networkUIPromptController, cameraRigController) ?? false); }
            catch (Exception exception)
            {
                RemasteredPlugin.Instance?.LogError("Command selection hook failed; opening vanilla picker", exception);
                return true;
            }
        }
    }
}


