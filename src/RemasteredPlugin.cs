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
        public const string Version = "0.4.1";
        internal const float ItemIconBaseSize = 51.2f;
        internal static RemasteredPlugin Instance;
        internal readonly QueueState Queue = new QueueState(ItemGroups.GroupOf);
        internal PresetStore Presets;
        internal readonly Dictionary<string, ConfigEntry<bool>> GroupOptions = new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        internal ConfigEntry<bool> RotateRepeatedItems;
        internal ConfigEntry<bool> DistributionEnabled, DistributeChests, DistributeExchangePools, DistributeTeleporterRewards, HexGroupsOfSeven;
        internal ConfigEntry<DrifterSalvageMode> DrifterSalvage;
        internal ConfigEntry<string> ExcludedDistributionStages;
        internal ConfigEntry<GameplayInputBlocking> BlockGameplayInput;
        internal ConfigEntry<float> ItemScale, MenuScale;
        internal ConfigEntry<SpecialPickupBehavior> TemporaryItemBehavior, EtherealItemBehavior;
        internal ConfigEntry<bool> IgnoreTemporaryItems, IgnoreEtherealItems;
        internal ConfigEntry<ExclusiveItemBehavior> ExclusiveItemSelection;
        internal ConfigEntry<bool> AutoCollectQueuedItems;
        internal ConfigEntry<bool> AutoPickupCommandEssence, ExcludePausedPlayers;
        private PausedPlayerSync pausedPlayerSync;
        private float autoPickupRetryAfter;
        private int lastEssenceWorkFrame = -1;
        internal ConfigEntry<InventoryCountMode> FewestItemsCount;
        internal ConfigEntry<LimiterItemCount> LimiterCountMode;
        internal readonly Dictionary<ItemTier, ConfigEntry<EssenceDropMode>> EssenceDropModes = new Dictionary<ItemTier, ConfigEntry<EssenceDropMode>>();
        internal Sprite ModIcon;
        internal Texture2D ModIconTexture;
        private Harmony harmony;
        private ProperSaveAdapter properSave;
        private readonly HashSet<PickupPickerController> pendingSelections = new HashSet<PickupPickerController>();
        private readonly HashSet<PickupPickerController> manualDisplays = new HashSet<PickupPickerController>();
        private static readonly System.Reflection.MethodInfo localParticipantGetter =
            AccessTools.PropertyGetter(typeof(NetworkUIPromptController), "currentLocalParticipant");
        internal static bool InteractionActive(NetworkUIPromptController prompt, LocalUser user) =>
            prompt && user != null && ReferenceEquals(localParticipantGetter.Invoke(prompt, null), user);
        private readonly List<ScoreboardQueueUI> scoreboardViews = new List<ScoreboardQueueUI>();
        private float nextScoreboardScan;
        private Inventory selectionInventory;
        private readonly Dictionary<Inventory, PendingItemSelections> pendingLimitedItems = new Dictionary<Inventory, PendingItemSelections>();

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
                RotateRepeatedItems = Config.Bind("General", "RotateRepeatedItems", true, "Move collected repeated items to the queue end.\n\nWhen disabled, keep displayed order and quantities while cycling through every queued item internally.");
                AutoCollectQueuedItems = BindAutoCollectOption();
                AutoPickupCommandEssence = Config.Bind("General", "AutoPickupCommandEssence", false,
                    "Automatically open nearby Command essences when your active queue has a matching item, including while the scoreboard is open.\n\nEmpty or paused queues are left alone. Other menus, dialogs, text entry and active interactions remain protected.\n\nExclusiveItemSelection Automatic alone does not open an essence; that item must also be in the queue.\n\nUses normal interaction range; does not collect the selected item unless AutoCollectQueuedItems is enabled.");
                LimiterCountMode = Config.Bind("General", "LimiterItemCount", LimiterItemCount.PermanentOnly,
                    "Item stacks counted toward each Limiter target.\n\nPermanentOnly: permanent items.\n\nIncludeTemporary: permanent and temporary items.\n\nIncludeEthereal: permanent and ethereal items.\n\nIncludeBoth: permanent, temporary and ethereal items.\n\nApplies to limit checks and owned/limit badges. Special pickup selection/consumption still follows TemporaryItemBehavior and EtherealItemBehavior, including UseQueueNormally.");
                LimiterCountMode.SettingChanged += (_, __) => pendingLimitedItems.Clear();
                ExcludePausedPlayers = Config.Bind("General", "ExcludePausedPlayers", false,
                    "Skip players whose queue group is paused when distributing essences.\n\nOther groups can still receive drops.\n\nHost setting. Remote players need the updated mod to share their pause state; unknown states remain eligible.\n\nIf everyone is excluded, keep the original drop location.");
                FewestItemsCount = Config.Bind("Essence drops", "FewestItemsCount", InventoryCountMode.AllItems,
                    "AllItems: count permanent stacks across all items.\n\nSameTier: count permanent stacks only in the essence's actual tier.\n\nTemporary and ethereal items do not count. Outstanding permanent essence assignments count until collected or cancelled.\n\nThe host's setting applies to all players in the run. Joining players' settings have no effect.\n\nWhen FewestItems is selected and multiple players share the lowest item count, essence drops alternate between those players. Each item tier tracks this order separately, even with AllItems.");
                const string rules = "Item Distribution Rules";
                DistributionEnabled = Config.Bind(rules, "Enabled", true,
                    "Enable item essence distribution.\n\nWhen off, keep original drop locations without changing per-tier modes, queue selection or collection settings. Host setting.");
                ExcludedDistributionStages = Config.Bind(rules, "ExcludedStages", StageExclusions.Defaults,
                    "Stages where item distribution keeps original drop locations. Use Select stages in RiskOfOptions to choose named environments.\n\nDefaults:\n• Bazaar Between Time (bazaar)\n• Computational Exchange (computationalexchange)\n\nThe config file accepts comma-separated internal scene names; empty allows every stage. Host setting; applies immediately.");
                var stageSelectorVersion = Config.Bind("Migration", "StageSelectorVersion", 0, "Internal stage-selector migration marker. Retains later user choices.");
                if (stageSelectorVersion.Value < 1)
                {
                    var stages = StageExclusions.Parse(ExcludedDistributionStages.Value);
                    stages.UnionWith(StageExclusions.Parse(StageExclusions.Defaults));
                    ExcludedDistributionStages.Value = StageExclusions.Encode(stages);
                    stageSelectorVersion.Value = 1;
                }
                DistributeChests = Config.Bind(rules, "Chests", true,
                    "Distribute Command essences from standard and option chests, including money shop terminals.\n\nWhen off, these drops stay at their original location. Host setting.");
                DistributeExchangePools = Config.Bind(rules, "ExchangePools", true,
                    "Distribute Command essences from cleansing pools and Hallowed cauldrons.\n\nWhen off, these drops stay at their original location. Host setting; custom mod sources may use other paths.");
                DistributeTeleporterRewards = Config.Bind(rules, "TeleporterBossRewards", true,
                    "Distribute teleporter boss reward essences.\n\nWhen off, rewards stay at their original location. Other boss groups keep their normal per-tier distribution. Host setting.");
                DrifterSalvage = BindDrifterSalvageOption();
                HexGroupsOfSeven = Config.Bind(rules, "HexGroupsOfSeven", false,
                    "Distribute Hex essences in blocks of seven, counting all permanent Hex items together.\n\nComplete an unfinished block before starting the next player. With several unfinished blocks, finish the closest to seven first and keep serving that player until complete.\n\nOutstanding assignments count. After a complete block, rotate to the next eligible living player. Overrides the Hex recipient mode except Original. Host setting.");
                foreach (ItemTier tier in Enum.GetValues(typeof(ItemTier)))
                {
                    if (ItemGroups.Group(tier) == null) continue;
                    string tierLabel = OptionsPresentation.Name("Essence drops", tier.ToString());
                    EssenceDropModes[tier] = Config.Bind("Essence drops", tier.ToString(), EssenceDropMode.Original,
                        "Command item essence location for " + tierLabel + ".\n\nOriginal: keep the source location.\n\nRoundRobin: alternate between living players.\n\nKiller: use the killing player (including owned minions).\n\nFewestItems: use FewestItemsCount.\n\nMissing/dead killer or non-kill source falls back to RoundRobin; no living players keeps the original drop.\n\nApplies to all Command item droplet sources, not equipment. Actual Void tiers are independent.\n\nHost/server setting, applies immediately; no client installation needed for relocation.");
                }
                BlockGameplayInput = Config.Bind("General", "BlockGameplayInput", GameplayInputBlocking.On,
                    "On: block gameplay in both scoreboard views.\n\nCommandQueueOnly: block only in Command Queue, allow gameplay in Players.\n\nOff: allow gameplay in both views, including while clicking, hovering, typing or confirming. UI actions can also trigger gameplay.\n\nThe actual pause menu retains native control. Applies immediately.");
                Queue.RotateRepeatedItems = RotateRepeatedItems.Value;
                Queue.IsPickupAllowed = PickupAllowed;
                RotateRepeatedItems.SettingChanged += (_, __) => Queue.RotateRepeatedItems = RotateRepeatedItems.Value;
                const string specialDescription = "ManualMenu: open the normal picker.\n\nUseQueueWithoutConsuming: select the next available queued item of the same tier without changing quantities, order or Repeat progress.\n\nUseQueueNormally: select and consume normally.\n\nSkip entries unavailable in this temporary or ethereal essence, leaving them queued. If no eligible queued item is available, open the normal picker.";
                TemporaryItemBehavior = Config.Bind("Special items", "TemporaryItemBehavior", SpecialPickupBehavior.ManualMenu, "Behavior for timed temporary Command pickups.\n\n" + specialDescription);
                EtherealItemBehavior = Config.Bind("Special items", "EtherealItemBehavior", SpecialPickupBehavior.ManualMenu, "Behavior for ethereal Command pickups.\n\n" + specialDescription);
                const string ignoreDescription = "Personal setting: exclude only your own player from distribution of this item type, independently of queue pause or Behaviour. The host's setting does not change other players' choices.\n\nAlso disable automatic proximity opening for this type. Manual interaction still follows its configured Behaviour.\n\nRequires updated host/client support to report remote preferences. Unknown or stale remote states remain eligible; with no eligible recipient, drops keep their original location.";
                IgnoreTemporaryItems = Config.Bind("Special items", "IgnoreTemporaryItems", false, "Ignore temporary Command items for distribution.\n\n" + ignoreDescription);
                IgnoreEtherealItems = Config.Bind("Special items", "IgnoreEtherealItems", false, "Ignore ethereal Command items for distribution.\n\n" + ignoreDescription);
                ExclusiveItemSelection = Config.Bind("Special items", "ExclusiveItemSelection", ExclusiveItemBehavior.Default,
                    "Halcyon Seed, Newly Hatched Zoea, Exposed Cerebellum, Encrypted Cerebellum, Prison Matrix and Sentry Key.\n\nDefault: select from their exclusive Command essence when queued at any position.\n\nAutomatic: also select without a queue entry after you interact with the essence.\n\nAutomatic does not open the essence by itself. AutoPickupCommandEssence opens it only if that item is also in your active queue.\n\nExisting entries follow normal consumption and Repeat rules.");
                MenuScale = Config.Bind("General", "MenuScale", 1f,
                    new ConfigDescription("Queue menu scale; capped to fit the screen.\n\nDefault: 1.0x.", new AcceptableValueRange<float>(0.5f, 2f)));
                ItemScale = Config.Bind("General", "ItemScale", 1f, new ConfigDescription("Item button scale.\n\n1.0x uses the compact 51.2-unit base size.", new AcceptableValueRange<float>(0.5f, 2f)));
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
                Logger.LogInfo("Scoreboard gameplay input hooks registered (Update and CanSendBodyInput).");
                LookingGlassCommandCompatibility.Register(harmony, this);
                Run.onRunStartGlobal += OnRunStart;
                Run.onRunDestroyGlobal += OnRunEnd;
                pausedPlayerSync = new PausedPlayerSync(this);
                RoR2Application.onLoad += RegisterOptions;
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
        private void RegisterOptions()
        {
            RoR2Application.onLoad -= RegisterOptions;
            OptionalOptions.Register(this, harmony);
        }
        private ConfigEntry<bool> BindAutoCollectOption()
        {
            bool save = Config.SaveOnConfigSet;
            ConfigEntry<bool> result;
            try
            {
                Config.SaveOnConfigSet = false;
                var previous = Config.Bind("Item collection", "AutoCollectQueuedItems", false, "Moved to General.");
                bool existing = previous.Value;
                Config.Remove(previous.Definition);
                result = Config.Bind("General", "AutoCollectQueuedItems", existing,
                    "Automatically collect the item selected from your queue.\n\nWithout this option, the selected item drops on the ground.\n\nIn multiplayer, enable this on both the host and your client.");
            }
            finally { Config.SaveOnConfigSet = save; }
            if (save) Config.Save();
            return result;
        }
        private ConfigEntry<DrifterSalvageMode> BindDrifterSalvageOption()
        {
            bool save = Config.SaveOnConfigSet;
            ConfigEntry<DrifterSalvageMode> result;
            try
            {
                Config.SaveOnConfigSet = false;
                // Read as text first so legacy True/False values do not fail enum parsing.
                var previous = Config.Bind("Item Distribution Rules", "DrifterSalvage", "SkillUser", "Migrated to a single choice.");
                var target = Config.Bind("Item Distribution Rules", "DrifterSalvageTarget", "", "Replaced by DrifterSalvage.");
                if (!DrifterSalvageSettings.TryMigrate(previous.Value, target.Value, out var mode))
                    LogWarning("Unrecognized Drifter Salvage setting; using Original.");
                Config.Remove(previous.Definition); Config.Remove(target.Definition);
                result = Config.Bind("Item Distribution Rules", "DrifterSalvage", DrifterSalvageMode.SkillUser,
                    "Location of temporary Command drops from Drifter's Salvage special skill.\n\nOriginal: keep the skill's original drop location.\n\nSkillUser (default): send all drops to the Drifter who used this skill, independently for each Drifter; overrides per-tier modes.\n\nItemDistribution: use the normal per-tier distribution mode.\n\nMaster switch, stage exclusions, personal Ignore and optional paused-group exclusions still apply. Without an eligible recipient, keep the original drop location. Other temporary sources and queue/collection settings are unaffected. Host setting.");
                result.Value = mode;
            }
            finally { Config.SaveOnConfigSet = save; }
            if (save) Config.Save();
            return result;
        }
        internal bool PlayerPaused(CharacterMaster master, string group) => pausedPlayerSync?.IsPaused(master, group) ?? false;
        internal bool PlayerIgnores(CharacterMaster master, UniquePickup pickup) => pausedPlayerSync?.IsIgnored(master, pickup.isTempItem, pickup.isRental) ?? false;
        internal void LogError(string context, Exception exception) => Logger.LogError(context + ": " + exception);
        internal void LogInfo(string message) => Logger.LogInfo(message);
        internal void LogWarning(string message) => Logger.LogWarning(message);
        internal bool HasPendingSelection(PickupPickerController picker) => pendingSelections.Contains(picker);
        private SpecialPickupBehavior SelectionBehavior(UniquePickup pickup) => SpecialPickupPolicy.Resolve(
            pickup.isTempItem, pickup.isRental, TemporaryItemBehavior.Value, EtherealItemBehavior.Value);
        internal bool GroupEnabled(string group) => group != null && GroupOptions.TryGetValue(group, out var option) && option.Value;
        internal MenuInputMode InputMode(LocalUser user) => scoreboardViews.Where(view => view)
            .Select(view => view.InputMode(user)).FirstOrDefault(mode => mode != MenuInputMode.KeepNative);
        internal bool BlocksGameplay(LocalUser user) => InputMode(user) == MenuInputMode.Block;
        internal bool SuppressPauseShortcut => scoreboardViews.Any(view => view && view.SuppressPauseShortcut);

        private void Update()
        {
            AttachScoreboards();
            properSave?.Update();
            pausedPlayerSync?.Update();
            FastAssignedEssence.Process();
        }
        private void FixedUpdate()
        {
            // Physics catch-up may run multiple ticks in one rendered frame. Keep the same work cap.
            if (lastEssenceWorkFrame == Time.frameCount) return;
            lastEssenceWorkFrame = Time.frameCount;
            if (Time.unscaledTime < autoPickupRetryAfter) return;
            try { TryPickupEssence(); }
            catch (Exception exception)
            {
                autoPickupRetryAfter = Time.unscaledTime + 5f;
                LogError("Automatic essence interaction failed; normal interaction remains available", exception);
            }
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
        private void OnRunStart(Run run) { Queue.Reset(); pendingSelections.Clear(); pendingLimitedItems.Clear(); selectionInventory = null; ResetDrops(); pausedPlayerSync?.Reset(); }
        private void OnRunEnd(Run run) { Queue.Reset(); pendingSelections.Clear(); pendingLimitedItems.Clear(); selectionInventory = null; ResetDrops(); pausedPlayerSync?.Reset(); }
        private static void ResetDrops()
        {
            EssenceDrops.Distribution.Reset();
            EssenceAssignments.Reset();
            EssenceDrops.Killer = null;
            DistributionSourceContext.Current = DropSource.Other; DistributionSourceContext.SkillUser = null;
            AutomaticCollection.Selecting.Clear();
            FastAssignedEssence.Reset();
            EssenceWorkRegistry.ResetInteractions();
        }
        private void OnDestroy()
        {
            RoR2Application.onLoad -= RegisterOptions;
            foreach (var view in scoreboardViews) if (view) Destroy(view);
            scoreboardViews.Clear();
            properSave?.Dispose();
            pausedPlayerSync?.Dispose();
            Run.onRunStartGlobal -= OnRunStart;
            Run.onRunDestroyGlobal -= OnRunEnd;
            harmony?.UnpatchSelf();
            ResetDrops();
            EssenceWorkRegistry.Clear();
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
            selectionInventory = user.cachedMaster ? user.cachedMaster.inventory : null;
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

        private bool HasQueuedChoice(PickupPickerController picker, LocalUser user)
        {
            selectionInventory = user.cachedMaster ? user.cachedMaster.inventory : null;
            if (!AutomaticCollection.IsCommand(picker) || picker.options == null) return false;
            uint ignored = SpecialPickupDistribution.IgnoreMask(IgnoreTemporaryItems.Value, IgnoreEtherealItems.Value);
            if (picker.options.Any(option => SpecialPickupDistribution.Ignores(ignored, option.pickup.isTempItem, option.pickup.isRental))) return false;
            var options = SelectionOptions(picker);
            return CommandSelection.HasQueuedChoice(Queue, options, SourceTier(picker), ItemGroups.TierOf,
                ItemGroups.IsExclusivePickup, ExclusiveItemSelection.Value);
        }
        private void TryPickupEssence()
        {
            if (AutoPickupCommandEssence == null || !AutoPickupCommandEssence.Value || !Run.instance ||
                Time.timeScale <= 0 || Queue.Entries.Count == 0 || pendingSelections.Count != 0) return;
            foreach (var user in LocalUserManager.readOnlyLocalUsersList)
            {
                var body = user.cachedBody;
                bool blockedByUi = user.isUIFocused && !scoreboardViews.Any(view => view && view.AllowsEssenceInteraction(user));
                if (!body || !body.healthComponent || !body.healthComponent.alive || blockedByUi || EssenceWorkRegistry.Busy(user)) continue;
                var interactor = body.GetComponent<Interactor>();
                if (!interactor) continue;
                int visits = Math.Min(EssenceWorkRegistry.DistanceBudget, EssenceWorkRegistry.Candidates.Count);
                int choices = 0;
                for (int i = 0; i < visits && choices < EssenceWorkRegistry.ChoiceBudget; i++)
                {
                    if (!EssenceWorkRegistry.Candidates.TryVisit(out var candidate)) break;
                    var picker = candidate.Picker;
                    if (!picker) { EssenceWorkRegistry.Candidates.Remove(candidate); continue; }
                    if (!picker.isActiveAndEnabled || Time.unscaledTime < candidate.RetryAt) continue;
                    var collider = candidate.Collider;
                    float rangeSquared = interactor.maxInteractionDistance * interactor.maxInteractionDistance;
                    if (collider && (!collider.enabled || collider.bounds.SqrDistance(body.corePosition) > rangeSquared)) continue;
                    // Use current geometry: OnEnable can precede final positioning/scaling.
                    Vector3 nearest = collider ? collider.ClosestPoint(body.corePosition) : picker.transform.position;
                    if ((nearest - body.corePosition).sqrMagnitude > rangeSquared) continue;
                    choices++;
                    if (!HasQueuedChoice(picker, user) || picker.GetInteractability(interactor) != Interactability.Available ||
                        // The center can lie behind terrain while the near surface remains reachable.
                        Physics.Linecast(body.corePosition, nearest, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) continue;
                    candidate.RetryAt = Time.unscaledTime + 1f;
                    EssenceWorkRegistry.Request(user);
                    interactor.AttemptInteraction(picker.gameObject);
                    break;
                }
            }
        }

        private CommandOption[] SelectionOptions(PickupPickerController picker) => picker.options.Select(option =>
            {
                var pickup = PickupCatalog.GetPickupDef(option.pickup.pickupIndex);
                var item = pickup == null ? null : ItemCatalog.GetItemDef(pickup.itemIndex);
                return new CommandOption(item ? pickup.internalName : null, item ? item.tier.ToString() : null,
                    option.available && item && GroupEnabled(ItemGroups.Group(item)) &&
                    (SelectionBehavior(option.pickup) == SpecialPickupBehavior.UseQueueNormally ||
                     SelectionBehavior(option.pickup) == SpecialPickupBehavior.UseQueueWithoutConsuming) &&
                    PickupAllowed(item ? pickup.internalName : null, LimiterCounting.PendingAmount(option.pickup.isTempItem, option.pickup.isRental, LimiterCountMode.Value)),
                    option.pickup.isTempItem || option.pickup.isRental);
            }).ToArray();
        private static string SourceTier(PickupPickerController picker)
        {
            var source = PickupCatalog.GetPickupDef(picker.GetPickupIndex());
            var sourceItem = source == null ? null : ItemCatalog.GetItemDef(source.itemIndex);
            return sourceItem ? sourceItem.tier.ToString() : null;
        }
        private int FindSelection(CommandOption[] options, string sourceTier) => CommandSelection.FindOption(
            Queue, options, sourceTier, ItemGroups.TierOf, ItemGroups.IsExclusivePickup, ExclusiveItemSelection.Value);
        private bool PickupAllowed(string pickup) => PickupAllowed(pickup, 1);
        private bool PickupAllowed(string pickup, int amount)
        {
            var item = ItemGroups.Definition(pickup);
            if (!item) return true;
            if (!selectionInventory) return true;
            long actual = CountForLimiter(selectionInventory, item);
            return !Queue.Limiter.Blocks(pickup, actual + PendingCounts(selectionInventory).Count(pickup, actual, Time.timeAsDouble) + Math.Max(0, amount - 1));
        }
        internal long CountForLimiter(Inventory inventory, ItemDef item) => inventory && item ? LimiterCounting.Count(
            inventory.GetItemCountPermanent(item.itemIndex), inventory.GetItemCountTemp(item.itemIndex), inventory.GetItemCountRented(item.itemIndex), LimiterCountMode.Value) : 0;
        private PendingItemSelections PendingCounts(Inventory inventory)
        {
            if (!pendingLimitedItems.TryGetValue(inventory, out var counts)) pendingLimitedItems[inventory] = counts = new PendingItemSelections();
            return counts;
        }
        private int SelectionHead(CommandOption[] options, string tier, bool resetRepeatCycle = false) =>
            CommandSelection.FindQueueEntry(Queue, options, tier, ItemGroups.TierOf, ItemGroups.IsExclusivePickup, resetRepeatCycle);

        private IEnumerator SubmitNextFrame(PickupPickerController picker, int optionIndex, string pickup, string tier,
            LocalUser user, NetworkUIPromptController prompt, CameraRigController camera, SpecialPickupBehavior behavior,
            bool exclusive, QueueEntry plannedEntry)
        {
            yield return null;
            bool submitted = false;
            PendingItemSelections.Ticket limitTicket = null;
            PendingItemSelections limitCounts = null;
            try
            {
                // A cancelled interaction must not submit a stale choice or reopen its panel.
                if (!picker || !InteractionActive(prompt, user)) yield break;
                if (Queue.IsPickupPaused(pickup) || picker.options == null || optionIndex >= picker.options.Length ||
                    !picker.options[optionIndex].available || PickupCatalog.GetPickupDef(picker.options[optionIndex].pickup.pickupIndex)?.internalName != pickup) yield break;
                selectionInventory = user.cachedMaster ? user.cachedMaster.inventory : null;
                var options = SelectionOptions(picker);
                if (FindSelection(options, SourceTier(picker)) != optionIndex ||
                    CommandSelection.IsExclusiveEssence(options, ItemGroups.IsExclusivePickup) != exclusive || options[optionIndex].Tier != tier) yield break;
                int head = SelectionHead(options, tier);
                if (!ReferenceEquals(plannedEntry, head < 0 ? null : Queue.Entries[head])) yield break;
                if (SelectionBehavior(picker.options[optionIndex].pickup) != behavior || !GroupEnabled(ItemGroups.GroupOf(pickup))) yield break;
                if (behavior == SpecialPickupBehavior.UseQueueNormally && head >= 0) head = SelectionHead(options, tier, true);
                var definition = ItemGroups.Definition(pickup);
                int limitAmount = LimiterCounting.PendingAmount(picker.options[optionIndex].pickup.isTempItem, picker.options[optionIndex].pickup.isRental, LimiterCountMode.Value);
                if (selectionInventory && definition && limitAmount > 0 && Queue.Limiter.Limit(pickup).HasValue)
                {
                    limitCounts = PendingCounts(selectionInventory);
                    limitTicket = limitCounts.Reserve(pickup, CountForLimiter(selectionInventory, definition), Time.timeAsDouble, limitAmount);
                }
                AutomaticCollection.Submit(picker, prompt, optionIndex, head >= 0);
                submitted = true;
                if (head >= 0) SpecialPickupPolicy.CompleteSelection(Queue, head, tier, behavior);
            }
            catch (Exception exception) { LogError("Automatic command selection failed; queue retained where possible", exception); }
            finally
            {
                pendingSelections.Remove(picker);
                if (!submitted) limitCounts?.Cancel(limitTicket);
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


