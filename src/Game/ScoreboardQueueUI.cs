using System;
using System.Collections.Generic;
using System.Linq;
using CommandQueueRemastered.Core;
using CommandQueueRemastered.Integrations;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CommandQueueRemastered.Game
{
    // Native child of the game's scoreboard. Visibility follows its existing key binding and lifecycle.
    internal sealed class ScoreboardQueueUI : MonoBehaviour
    {
        private RemasteredPlugin plugin;
        private HUD hud;
        private ScoreboardController scoreboard;
        private RectTransform root, panel, queueContent, itemContent, saveContent, marker, ghost;
        private ScrollRect queueScroll, itemScroll, descriptionScroll, saveScroll, previewScroll;
        private RectTransform previewContent, previewHeader, saveActions;
        private RectTransform shortcutHelp;
        private TextMeshProUGUI shortcutClickHelp;
        private TextMeshProUGUI description, previewTitle, ghostCount, repeatLabel;
        private TMP_StyleSheet styleSheet;
        private readonly List<Button> selectedSaveActions = new List<Button>();
        private readonly List<QueuePreviewIcon> previewIcons = new List<QueuePreviewIcon>();
        private Image ghostImage;
        private Image ghostVoidMarker;
        private Image repeatImage;
        private RectTransform clearConfirmation;
        private Button cancelClearButton;
        private CanvasGroup panelInput, toolbarInput;
        private GameObject selectionBeforeConfirmation;
        private int confirmationEscapeFrame = -1;
        private static readonly Color NeutralActionColor = new Color(0.16f, 0.19f, 0.24f, 1);
        private static readonly Color RepeatActionColor = new Color(0.19f, 0.32f, 0.23f, 1);
        private static readonly Color ClearActionColor = new Color(0.34f, 0.19f, 0.20f, 1);
        internal bool SuppressPauseShortcut => confirmationEscapeFrame == Time.frameCount ||
            (clearConfirmation && clearConfirmation.gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape));
        private TMP_InputField nameInput;
        private TMP_FontAsset font;
        private ItemIconCache icons;
        private ItemDescriptions descriptions;
        private readonly List<QueueItemButton> tiles = new List<QueueItemButton>();
        private readonly List<QueueItemButton> queuedTiles = new List<QueueItemButton>();
        private readonly List<GameObject> originalChildren = new List<GameObject>();
        private readonly List<bool> originalActive = new List<bool>();
        private readonly Dictionary<Graphic, bool> originalGraphics = new Dictionary<Graphic, bool>();
        private readonly Dictionary<string, Button> groupButtons = new Dictionary<string, Button>();
        private MPEventSystem cursorSystem;
        private bool hiddenOriginals, queueView, dirty = true, cursorOwned, failed;
        private string group = "White", renameId, selectedSaveId, settingsSignature;
        private ItemDef described;
        private QueuePreset previewPreset;
        private QueueItemButton dragged;
        private int dropBoundary = -1;
        private float nextRefresh;
        private const float Width = 1280, Height = 720;
        private LocalUser User => hud ? hud.localUserViewer : null;
        public bool BlocksGameplay(LocalUser user) => user != null && user == User && !failed && isActiveAndEnabled &&
            HUD.IsScoreboardActive(user);
        private Camera EventCamera => root.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
        private static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        private static bool Control => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        public void Initialize(RemasteredPlugin owner, HUD ownerHud, ScoreboardController controller)
        {
            plugin = owner; hud = ownerHud; scoreboard = controller;
            foreach (Transform child in controller.transform)
            { originalChildren.Add(child.gameObject); originalActive.Add(child.gameObject.activeSelf); }
            font = controller.GetComponentsInChildren<TextMeshProUGUI>(true).Select(text => text.font).FirstOrDefault(asset => asset);
            if (!font) font = TMP_Settings.defaultFontAsset;
            if (!font) throw new InvalidOperationException("No scoreboard font is available.");
            styleSheet = controller.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Select(text => text.styleSheet).FirstOrDefault(sheet => sheet);
            if (!styleSheet) styleSheet = TMP_Settings.defaultStyleSheet;
            icons = new ItemIconCache(plugin); descriptions = new ItemDescriptions(plugin);
            root = Rect("CommandQueueRemastered", controller.transform);
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f); root.sizeDelta = new Vector2(Width, Height);
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 40;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var toolbar = Box("Views", root, new Vector2(0, 0.96f), Vector2.one);
            toolbarInput = toolbar.gameObject.AddComponent<CanvasGroup>();
            Place(Button(toolbar, "Players", () => { queueView = false; ApplyVisibility(); }), 0, 0, 0.16f, 1);
            Place(Button(toolbar, "Command Queue", () => { queueView = true; ApplyVisibility(); }), 0.17f, 0, 0.42f, 1);
            var title = Label(toolbar, "CommandQueue Remastered " + RemasteredPlugin.Version, 18);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            Place(title.rectTransform, 0.44f, 0, 1, 1);
            panel = Box("Queue panel", root, Vector2.zero, new Vector2(1, 0.95f));
            panelInput = panel.gameObject.AddComponent<CanvasGroup>();
            var groups = Rect("Groups", panel); Place(groups, 0.01f, 0.947f, 0.99f, 0.989f);
            for (int index = 0; index < ItemGroups.Names.Length; index++)
            {
                string name = ItemGroups.Names[index];
                var button = Button(groups, name, () => { group = name; described = null; dirty = true; CancelDrag(); });
                Place(button, index / 9f, 0, (index + 0.94f) / 9f, 1);
                groupButtons.Add(name, button.GetComponent<Button>());
            }
            var actions = Rect("Queue actions", panel); Place(actions, 0.01f, 0.895f, 0.68f, 0.937f);
            var repeatButton = Button(actions, "Repeat", ToggleRepeat);
            repeatLabel = repeatButton.GetComponentInChildren<TextMeshProUGUI>();
            repeatImage = repeatButton.GetComponent<Image>();
            Place(repeatButton, 0, 0, 0.22f, 1);
            Place(Button(actions, "Clear group", () => Act(() => plugin.Queue.Replace(
                plugin.Queue.Entries.Where(entry => ItemGroups.GroupOf(entry.Pickup) != group).Select(entry => entry.Copy()).ToList(),
                plugin.Queue.RepeatedTiers.ToArray()))), 0.23f, 0, 0.46f, 1);
            var clearButton = Button(actions, "Clear all", OpenClearConfirmation);
            clearButton.GetComponent<Image>().color = ClearActionColor;
            Place(clearButton, 0.47f, 0, 0.68f, 1);
            queueScroll = Scroll(panel, "Queued items", 0.01f, 0.72f, 0.68f, 0.88f, out queueContent);
            var queueGrid = queueContent.gameObject.AddComponent<GridLayoutGroup>();
            queueGrid.spacing = new Vector2(7, 7); queueGrid.padding = new RectOffset(8, 8, 8, 8);
            queueGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            Fit(queueContent, false);
            queueScroll.horizontalScrollbar.gameObject.SetActive(false);
            queueScroll.viewport.offsetMin = Vector2.zero;
            shortcutHelp = Rect("Queue shortcuts", panel);
            var clickHelp = Label(shortcutHelp, QueueInteraction.ClickHelp + "\n" + QueueInteraction.RemoveHelp, 14);
            shortcutClickHelp = clickHelp;
            Place(clickHelp.rectTransform, 0, 0, 0.60f, 1);
            clickHelp.enableWordWrapping = false;
            clickHelp.enableAutoSizing = true; clickHelp.fontSizeMin = 12; clickHelp.fontSizeMax = 14;
            var moveHelp = Label(shortcutHelp, QueueInteraction.DragHelp, 14);
            Place(moveHelp.rectTransform, 0.61f, 0.5f, 1, 1);
            moveHelp.alignment = TextAlignmentOptions.TopRight;
            moveHelp.enableWordWrapping = false;
            moveHelp.enableAutoSizing = true; moveHelp.fontSizeMin = 12; moveHelp.fontSizeMax = 14;
            itemScroll = Scroll(panel, "Available items", 0.01f, 0.26f, 0.68f, 0.70f, out itemContent);
            itemScroll.horizontalScrollbar.gameObject.SetActive(false);
            itemScroll.viewport.offsetMin = Vector2.zero;
            var grid = itemContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(7, 7); grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; Fit(itemContent, false);
            descriptionScroll = Scroll(panel, "Description", 0.01f, 0.015f, 0.68f, 0.24f, out var descriptionContent);
            description = Label(descriptionContent, "Hover an item for its description.", 17);
            description.enableWordWrapping = true;
            description.overflowMode = TextOverflowModes.Overflow;
            // TMP's unwrapped preferred width must not enlarge the description beyond its viewport.
            // The parent controls width; TMP supplies the wrapped preferred height for vertical scrolling.
            var descriptionSize = description.gameObject.AddComponent<LayoutElement>();
            descriptionSize.minWidth = 0; descriptionSize.preferredWidth = 0; descriptionSize.flexibleWidth = 1;
            var descriptionLayout = descriptionContent.gameObject.AddComponent<VerticalLayoutGroup>();
            descriptionLayout.padding = new RectOffset(8, 8, 8, 8);
            descriptionLayout.childControlWidth = true; descriptionLayout.childForceExpandWidth = true;
            descriptionLayout.childControlHeight = true; descriptionLayout.childForceExpandHeight = false;
            Fit(descriptionContent, false);
            var saveHeader = Rect("Preset input", panel); Place(saveHeader, 0.70f, 0.895f, 0.99f, 0.937f);
            nameInput = InputField(saveHeader);
            Place(nameInput.GetComponent<RectTransform>(), 0, 0, 0.65f, 1);
            Place(Button(saveHeader, "Save / Rename", Save), 0.67f, 0, 1, 1);
            saveScroll = Scroll(panel, "Presets", 0.70f, 0.075f, 0.99f, 0.88f, out saveContent);
            var savesLayout = saveContent.gameObject.AddComponent<VerticalLayoutGroup>();
            savesLayout.spacing = 4; savesLayout.padding = new RectOffset(6, 6, 6, 6);
            savesLayout.childControlHeight = true; savesLayout.childForceExpandHeight = false; Fit(saveContent, false);
            saveActions = Rect("Selected preset actions", panel);
            AddSaveAction("Load", () => Act(() => SelectedSave.Document.ApplyGroups(plugin.Queue, ItemGroups.GroupOf, ItemGroups.GroupOfTier)));
            AddSaveAction("Preview", () => { previewPreset = SelectedSave; UpdatePreview(); });
            AddSaveAction("Rename", () => { renameId = SelectedSave.Id; nameInput.text = SelectedSave.Name; nameInput.Select(); nameInput.ActivateInputField(); });
            AddSaveAction("Delete", () => Act(() =>
            {
                if (plugin.presetsReadFailed) throw new InvalidOperationException("Preset writes are disabled after a read failure.");
                plugin.Presets.Delete(SelectedSave.Id); selectedSaveId = null;
                renameId = null; nameInput.text = ""; UpdatePreview();
            }));
            previewHeader = Rect("Preview header", panel); Place(previewHeader, 0.70f, 0.335f, 0.99f, 0.377f);
            previewTitle = Label(previewHeader, "", 16); Place(previewTitle.rectTransform, 0, 0, 0.72f, 1);
            Place(Button(previewHeader, "Close", () => { previewPreset = null; UpdatePreview(); }), 0.74f, 0, 1, 1);
            previewScroll = Scroll(panel, "Preset preview", 0.70f, 0.015f, 0.99f, 0.325f, out previewContent);
            Fit(previewContent, false);
            var previewLayout = previewContent.gameObject.AddComponent<GridLayoutGroup>();
            previewLayout.spacing = new Vector2(6, 6); previewLayout.padding = new RectOffset(6, 6, 6, 6);
            previewLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            UpdatePreview();
            marker = Rect("Insertion marker", queueContent);
            marker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            marker.gameObject.AddComponent<Image>().color = new Color(0.8f, 0.55f, 1f);
            marker.GetComponent<Image>().raycastTarget = false; marker.gameObject.SetActive(false);
            ghost = Rect("Dragged item", root); ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0, 1); ghost.sizeDelta = new Vector2(76, 76);
            ghostImage = ghost.gameObject.AddComponent<Image>(); ghostImage.raycastTarget = false;
            var ghostStrip = Rect("Void tier color", ghost); Place(ghostStrip, 0, 0.96f, 1, 1);
            ghostVoidMarker = ghostStrip.gameObject.AddComponent<Image>(); ghostVoidMarker.raycastTarget = false;
            ghostCount = Label(ghost, "", 20); Place(ghostCount.rectTransform, 0, 0, 1, 0.33f);
            ghostCount.alignment = TextAlignmentOptions.Center; ghost.gameObject.SetActive(false);
            BuildClearConfirmation();
            plugin.Queue.Changed += MarkDirty;
            ApplyVisibility();
            plugin.LogInfo("Native queue UI attached to scoreboard; UI layer=" + root.gameObject.layer +
                ", canvas=" + root.GetComponentInParent<Canvas>().rootCanvas.renderMode + ", initial view=Players.");
        }

        internal void DisableAfterFailure()
        {
            failed = true; CancelDrag(); ReleaseCursor(); RestorePlayers();
            if (root) root.gameObject.SetActive(false);
            enabled = false;
        }
        private void MarkDirty() { dirty = true; }
        private void OnEnable() { if (plugin) { dirty = true; ApplyVisibility(); } }
        private void OnDisable() { CloseClearConfirmation(); CancelDrag(); ReleaseCursor(); RestorePlayers(); }
        private void OnDestroy()
        {
            if (plugin) plugin.Queue.Changed -= MarkDirty;
            ReleaseCursor(); RestorePlayers(); icons?.Dispose();
            if (root) Destroy(root.gameObject);
        }
        private void RestorePlayers()
        {
            if (!hiddenOriginals) return;
            foreach (var pair in originalGraphics) if (pair.Key) pair.Key.enabled = pair.Value;
            originalGraphics.Clear();
            for (int index = 0; index < originalChildren.Count; index++)
                if (originalChildren[index]) originalChildren[index].SetActive(originalActive[index]);
            hiddenOriginals = false;
        }
        private void HidePlayers()
        {
            // Leave the native scoreboard hierarchy alive: its background effects
            // (including post-processing volumes) must run in both views.
            foreach (var child in originalChildren)
            {
                if (!child) continue;
                foreach (var graphic in child.GetComponentsInChildren<Graphic>(true))
                {
                    var material = graphic.material;
                    bool backgroundEffect = material && material.shader && material.shader.name.IndexOf("blur", StringComparison.OrdinalIgnoreCase) >= 0;
                    for (Transform ancestor = graphic.transform; ancestor && ancestor != scoreboard.transform; ancestor = ancestor.parent)
                        backgroundEffect |= ancestor.name.IndexOf("blur", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (backgroundEffect) continue;
                    if (!originalGraphics.ContainsKey(graphic)) originalGraphics.Add(graphic, graphic.enabled);
                    graphic.enabled = false;
                }
            }
        }
        private void ReleaseCursor()
        {
            if (cursorOwned && cursorSystem)
            {
                // LocalUser.isUIFocused also checks selected objects. Hidden queue buttons
                // must not keep gameplay focused until the next mouse click.
                nameInput?.DeactivateInputField();
                var selected = cursorSystem.currentSelectedGameObject;
                if (selected && (!selected.activeInHierarchy || (scoreboard && selected.transform.IsChildOf(scoreboard.transform)))) selected = null;
                cursorSystem.SetSelectedGameObject(selected);
                cursorSystem.cursorOpenerCount = Math.Max(0, cursorSystem.cursorOpenerCount - 1);
                var inputModule = cursorSystem.currentInputModule;
                if (inputModule && inputModule.isActiveAndEnabled)
                { inputModule.DeactivateModule(); inputModule.ActivateModule(); }
                // ActivateModule can pick firstSelectedGameObject again. Keep only an
                // existing selection belonging to another still-open native menu.
                cursorSystem.SetSelectedGameObject(selected);
            }
            cursorOwned = false; cursorSystem = null;
        }
        private void ApplyVisibility()
        {
            if (!root) return;
            bool available = !failed &&
                User != null && HUD.IsScoreboardActive(User) && gameObject.activeInHierarchy;
            bool wasAvailable = root.gameObject.activeSelf;
            root.gameObject.SetActive(available);
            if (available && !wasAvailable) dirty = true;
            if (!available) { CloseClearConfirmation(); panel.gameObject.SetActive(false); RestorePlayers(); ReleaseCursor(); return; }
            if (!cursorOwned && User.eventSystem)
            { cursorSystem = User.eventSystem; cursorSystem.cursorOpenerCount++; cursorOwned = true; }
            if (!queueView) { CloseClearConfirmation(); panel.gameObject.SetActive(false); RestorePlayers(); CancelDrag(); return; }
            panel.gameObject.SetActive(true);
            if (!hiddenOriginals)
            {
                for (int index = 0; index < originalChildren.Count; index++)
                    if (originalChildren[index]) originalActive[index] = originalChildren[index].activeSelf;
                hiddenOriginals = true;
            }
            HidePlayers();
        }
        private void Update()
        {
            if (!plugin || failed) return;
            try
            {
                ApplyVisibility();
                if (!root.gameObject.activeInHierarchy) return;
                if (clearConfirmation.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                { confirmationEscapeFrame = Time.frameCount; CloseClearConfirmation(); }
                // The scoreboard container may size itself to the player rows. Fit our fixed
                // layout to the HUD canvas instead, including when only the view tabs are shown.
                var parent = root.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
                float scale = Mathf.Max(0.1f, Mathf.Min(plugin.MenuScale.Value, (parent.rect.width - 20) / Width, (parent.rect.height - 20) / Height));
                root.localScale = Vector3.one * scale;
                if (!panel.gameObject.activeSelf) return;
                UpdateQueueLayout();
                string signature = string.Join(",", ItemGroups.Names.Where(plugin.GroupEnabled)) + ":" + plugin.ItemScale.Value;
                if (signature != settingsSignature) { dirty = true; settingsSignature = signature; }
                if (dragged)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(queueScroll.viewport, Input.mousePosition, EventCamera) &&
                        queueContent.rect.height > queueScroll.viewport.rect.height)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(queueScroll.viewport, Input.mousePosition, EventCamera, out var pointer);
                        float edge = Mathf.Min(24, queueScroll.viewport.rect.height / 4);
                        float direction = pointer.y > queueScroll.viewport.rect.yMax - edge ? 1 :
                            pointer.y < queueScroll.viewport.rect.yMin + edge ? -1 : 0;
                        queueScroll.verticalNormalizedPosition = Mathf.Clamp01(queueScroll.verticalNormalizedPosition + direction * Time.unscaledDeltaTime);
                    }
                    UpdateDrag(Input.mousePosition);
                }
                if (Time.unscaledTime < nextRefresh) return;
                nextRefresh = Time.unscaledTime + 0.2f;
                if (dirty && !dragged) { Rebuild(); dirty = false; }
                foreach (var tile in tiles.ToArray()) if (tile && tile.Item) tile.Refresh();
                foreach (var tile in previewIcons) if (tile) tile.Refresh();
                UpdateDescription();
            }
            catch (Exception exception)
            {
                failed = true; CancelDrag(); ReleaseCursor(); RestorePlayers();
                if (root) root.gameObject.SetActive(false);
                plugin.LogError("Native scoreboard queue failed; queue view disabled and player overview restored", exception);
            }
        }
        private void Rebuild()
        {
            Canvas.ForceUpdateCanvases();
            if (!plugin.GroupEnabled(group)) group = ItemGroups.Names.FirstOrDefault(plugin.GroupEnabled) ?? "";
            foreach (var pair in groupButtons) { pair.Value.gameObject.SetActive(plugin.GroupEnabled(pair.Key)); pair.Value.interactable = pair.Key != group; }
            foreach (var tile in tiles) if (tile) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); }
            tiles.Clear(); queuedTiles.Clear();
            foreach (var entry in plugin.Queue.Entries)
            {
                var item = ItemGroups.Definition(entry.Pickup);
                if (item && ItemGroups.Group(item) == group) queuedTiles.Add(CreateTile(queueContent, item, entry));
            }
            var grid = itemContent.GetComponent<GridLayoutGroup>();
            float size = RemasteredPlugin.ItemIconBaseSize * plugin.ItemScale.Value;
            grid.cellSize = Vector2.one * size;
            grid.constraintCount = Math.Max(1, (int)((itemScroll.viewport.rect.width - 16) / (size + 7)));
            var queueGrid = queueContent.GetComponent<GridLayoutGroup>();
            queueGrid.cellSize = grid.cellSize; queueGrid.constraintCount = grid.constraintCount;
            foreach (var item in ItemGroups.Available(group)) CreateTile(itemContent, item, null);
            foreach (Transform child in saveContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (SelectedSave == null) selectedSaveId = null;
            foreach (var preset in plugin.Presets.Presets)
            {
                var row = Button(saveContent, "", () => SelectSave(preset.Id));
                row.name = "Preset " + preset.Name;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
                row.GetComponent<Image>().color = preset.Id == selectedSaveId
                    ? new Color(0.27f, 0.32f, 0.42f, 1) : new Color(0.12f, 0.15f, 0.21f, 1);
                var label = row.GetComponentInChildren<TextMeshProUGUI>();
                label.text = "<noparse>" + preset.Name + "</noparse>\n<size=13>" + PresetTotals(preset) + "</size>";
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(6, 0); label.rectTransform.offsetMax = new Vector2(-6, 0);
            }
            foreach (var button in selectedSaveActions) button.interactable = SelectedSave != null;
            UpdatePreview();
            var tiers = ItemGroups.Available(group).Select(item => item.tier.ToString()).Distinct().ToArray();
            bool repeating = tiers.Length > 0 && tiers.All(plugin.Queue.IsRepeated);
            repeatLabel.text = repeating ? "Repeat: on" : "Repeat: off";
            repeatImage.color = repeating ? RepeatActionColor : NeutralActionColor;
            if (!clearConfirmation.gameObject.activeSelf && User?.eventSystem && tiles.Count > 0)
            {
                var selected = User.eventSystem.currentSelectedGameObject;
                if (!selected || !selected.transform.IsChildOf(root))
                    User.eventSystem.SetSelectedGameObject(tiles[0].gameObject);
            }
        }
        private void UpdateQueueLayout()
        {
            float size = RemasteredPlugin.ItemIconBaseSize * plugin.ItemScale.Value;
            float bottom = 0.88f - (2 * size + 7 + 16) / panel.rect.height;
            Place(queueScroll.GetComponent<RectTransform>(), 0.01f, bottom, 0.68f, 0.88f);
            float helpHeight = Mathf.Ceil(shortcutClickHelp.preferredHeight);
            Place(shortcutHelp, 0.015f, bottom - (4 + helpHeight) / panel.rect.height, 0.68f, bottom - 4 / panel.rect.height);
            Place(itemScroll.GetComponent<RectTransform>(), 0.01f, 0.26f, 0.68f, bottom - (8 + helpHeight) / panel.rect.height);
        }
        private QueueItemButton CreateTile(RectTransform parent, ItemDef item, QueueEntry entry)
        {
            var rect = Rect("Item " + item.name, parent); rect.sizeDelta = Vector2.one * (RemasteredPlugin.ItemIconBaseSize * plugin.ItemScale.Value);
            var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(0.12f, 0.15f, 0.21f, 1);
            var tile = rect.gameObject.AddComponent<QueueItemButton>(); tile.targetGraphic = background;
            var graphic = Rect("Icon", rect); Stretch(graphic); graphic.offsetMin = new Vector2(4, 4); graphic.offsetMax = new Vector2(-4, -4);
            var image = graphic.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
            AddVoidMarker(rect, item);
            var count = Label(rect, "", 18); Place(count.rectTransform, 0, 0, 1, 0.32f); count.alignment = TextAlignmentOptions.BottomRight;
            tile.Initialize(this, item, entry, image, count); tiles.Add(tile); return tile;
        }
        private void Save() => Act(() =>
        {
            if (plugin.presetsReadFailed) throw new InvalidOperationException("Preset writes are disabled after a read failure.");
            if (renameId == null) plugin.Presets.Add(nameInput.text, plugin.Queue); else plugin.Presets.Rename(renameId, nameInput.text);
            renameId = null; nameInput.text = "";
        });
        private void BuildClearConfirmation()
        {
            clearConfirmation = Box("Clear all confirmation", root, Vector2.zero, Vector2.one);
            clearConfirmation.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
            var dialog = Box("Confirmation", clearConfirmation, new Vector2(0.28f, 0.36f), new Vector2(0.72f, 0.64f));
            var question = Label(dialog, "Clear all queues?", 22);
            question.alignment = TextAlignmentOptions.Center;
            Place(question.rectTransform, 0.04f, 0.65f, 0.96f, 0.93f);
            var detail = Label(dialog, "All queued items will be removed.", 17);
            detail.alignment = TextAlignmentOptions.Center;
            Place(detail.rectTransform, 0.04f, 0.39f, 0.96f, 0.64f);
            var cancel = Button(dialog, "Cancel", CloseClearConfirmation);
            cancelClearButton = cancel.GetComponent<Button>();
            Place(cancel, 0.53f, 0.08f, 0.94f, 0.30f);
            var confirm = Button(dialog, "Clear all", () => { CloseClearConfirmation(); Act(plugin.Queue.Clear); });
            confirm.GetComponent<Image>().color = ClearActionColor;
            Place(confirm, 0.06f, 0.08f, 0.47f, 0.30f);
            clearConfirmation.gameObject.SetActive(false);
        }
        private void OpenClearConfirmation()
        {
            CancelDrag();
            selectionBeforeConfirmation = User?.eventSystem ? User.eventSystem.currentSelectedGameObject : null;
            nameInput.DeactivateInputField();
            panelInput.interactable = toolbarInput.interactable = false;
            panelInput.blocksRaycasts = toolbarInput.blocksRaycasts = false;
            clearConfirmation.SetAsLastSibling();
            clearConfirmation.gameObject.SetActive(true);
            if (User?.eventSystem) User.eventSystem.SetSelectedGameObject(cancelClearButton.gameObject);
        }
        private void CloseClearConfirmation()
        {
            if (!clearConfirmation || !clearConfirmation.gameObject.activeSelf) return;
            clearConfirmation.gameObject.SetActive(false);
            panelInput.interactable = toolbarInput.interactable = true;
            panelInput.blocksRaycasts = toolbarInput.blocksRaycasts = true;
            if (User?.eventSystem)
                User.eventSystem.SetSelectedGameObject(selectionBeforeConfirmation && selectionBeforeConfirmation.activeInHierarchy
                    ? selectionBeforeConfirmation : null);
            selectionBeforeConfirmation = null;
        }
        private void ToggleRepeat() => Act(() =>
        {
            var tiers = ItemGroups.Available(group).Select(item => item.tier.ToString()).Distinct().ToArray();
            bool on = tiers.Length > 0 && tiers.All(plugin.Queue.IsRepeated);
            foreach (var tier in tiers) if (plugin.Queue.IsRepeated(tier) == on) plugin.Queue.ToggleRepeat(tier);
        });
        private void Act(Action action)
        {
            try { action(); dirty = true; }
            catch (Exception exception) { plugin.LogError("Scoreboard queue action failed", exception); }
        }
        private void UpdatePreview()
        {
            if (previewPreset != null) previewPreset = plugin.Presets.Presets.FirstOrDefault(preset => preset.Id == previewPreset.Id);
            bool visible = previewPreset != null;
            previewHeader.gameObject.SetActive(visible); previewScroll.gameObject.SetActive(visible);
            Place(saveScroll.GetComponent<RectTransform>(), 0.70f, visible ? 0.445f : 0.075f, 0.99f, 0.88f);
            Place(saveActions, 0.70f, visible ? 0.393f : 0.023f, 0.99f, visible ? 0.435f : 0.065f);
            foreach (Transform child in previewContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            previewIcons.Clear();
            if (!visible) return;
            previewTitle.text = "<noparse>" + previewPreset.Name + "</noparse>";
            previewScroll.verticalNormalizedPosition = 1;
            var grid = previewContent.GetComponent<GridLayoutGroup>();
            float size = RemasteredPlugin.ItemIconBaseSize * 0.75f * plugin.ItemScale.Value;
            grid.cellSize = Vector2.one * size;
            grid.constraintCount = Math.Max(1, (int)((previewScroll.viewport.rect.width - 12) / (size + 6)));
            foreach (var entry in previewPreset.Document.Queue)
            {
                var rect = Box("Preview item " + entry.Pickup, previewContent, Vector2.zero, Vector2.one);
                var icon = Rect("Icon", rect); Stretch(icon); icon.offsetMin = new Vector2(3, 3); icon.offsetMax = new Vector2(-3, -3);
                var image = icon.gameObject.AddComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
                AddVoidMarker(rect, ItemGroups.Definition(entry.Pickup));
                var count = Label(rect, "×" + entry.Count, 16); Place(count.rectTransform, 0, 0, 1, 0.35f);
                count.alignment = TextAlignmentOptions.BottomRight;
                var tile = rect.gameObject.AddComponent<QueuePreviewIcon>();
                tile.Initialize(this, ItemGroups.Definition(entry.Pickup), image);
                previewIcons.Add(tile);
                if (!ItemGroups.Definition(entry.Pickup))
                { var missing = Label(rect, "?", 24); Stretch(missing.rectTransform); missing.alignment = TextAlignmentOptions.Center; }
            }
        }
        private QueuePreset SelectedSave => plugin.Presets.Presets.FirstOrDefault(preset => preset.Id == selectedSaveId);
        private void AddSaveAction(string text, Action action)
        {
            int index = selectedSaveActions.Count;
            var rect = Button(saveActions, text, () => { if (SelectedSave != null) action(); });
            Place(rect, index / 4f, 0, (index + 0.96f) / 4f, 1);
            selectedSaveActions.Add(rect.GetComponent<Button>());
        }
        private void SelectSave(string id)
        {
            selectedSaveId = id; renameId = null; nameInput.text = ""; dirty = true;
            if (previewPreset != null) { previewPreset = SelectedSave; UpdatePreview(); }
        }
        private static string PresetTotals(QueuePreset preset)
        {
            var entries = preset.Document.Queue;
            var parts = new List<string>();
            foreach (var name in ItemGroups.Names)
            {
                var matching = entries.Where(entry => ItemGroups.GroupOf(entry.Pickup) == name).ToArray();
                long count = matching.Sum(entry => (long)entry.Count);
                if (count == 0) continue;
                var tier = ItemTierCatalog.GetItemTierDef(ItemGroups.Definition(matching[0].Pickup).tier);
                string color = tier ? ColorCatalog.GetColorHexString(tier.colorIndex) : "FFFFFF";
                parts.Add("<color=#" + color + ">" + count + "</color>");
            }
            long total = entries.Sum(entry => (long)entry.Count);
            if (total > 0) parts.Add("Total " + total);
            return string.Join("  ", parts);
        }
        internal void Describe(ItemDef item)
        {
            if (described != item) { described = item; descriptionScroll.verticalNormalizedPosition = 1; }
            UpdateDescription();
        }
        private void UpdateDescription()
        {
            if (!described) return;
            description.text = "<b>" + Language.GetString(described.nameToken) + "</b>\n" +
                DescriptionFormatting.ForTmp(descriptions.Get(described, User?.cachedMaster));
        }
        internal void ScrollDescription(PointerEventData data, bool queued)
        {
            Canvas.ForceUpdateCanvases();
            if (descriptionScroll.content.rect.height > descriptionScroll.viewport.rect.height)
                descriptionScroll.OnScroll(data);
            else if (queued) queueScroll.OnScroll(data); else itemScroll.OnScroll(data);
        }
        internal void Click(QueueItemButton tile, PointerEventData.InputButton button)
        {
            Describe(tile.Item);
            Act(() =>
            {
                int index = plugin.Queue.Entries.ToList().IndexOf(tile.Entry);
                if (button == PointerEventData.InputButton.Right)
                { if (index >= 0) plugin.Queue.RemoveQuantity(index, QueueInteraction.DecreaseCount(tile.Entry.Count, Shift, Control)); }
                else if (button == PointerEventData.InputButton.Left)
                { if (index >= 0 && !Control) plugin.Queue.Increase(index, QueueInteraction.ClickCount(Shift)); else plugin.Queue.Enqueue(ItemGroups.PickupName(tile.Item), QueueInteraction.ClickCount(Shift), Control); }
            });
        }
        internal Sprite Sprite(ItemDef item) => icons.Get(item);
        internal void ScrollPreview(PointerEventData data) => previewScroll.OnScroll(data);
        internal int Count(ItemDef item) => User?.cachedMaster && User.cachedMaster.inventory ? User.cachedMaster.inventory.GetItemCountPermanent(item.itemIndex) : 0;
        internal void BeginDrag(QueueItemButton tile, PointerEventData data)
        {
            if (tile.Entry == null || data.button != PointerEventData.InputButton.Left) return;
            dragged = tile; ghostImage.sprite = icons.Get(tile.Item); ghost.gameObject.SetActive(true); UpdateDrag(data.position);
        }
        internal void UpdateDrag(Vector2 pointer)
        {
            if (!dragged) return;
            ghostImage.sprite = icons.Get(dragged.Item);
            bool isVoid = ItemGroups.TryVoidMarkerColor(dragged.Item, out var color);
            ghostVoidMarker.gameObject.SetActive(isVoid);
            if (isVoid) ghostVoidMarker.color = color;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer, EventCamera, out var local);
            ghost.anchoredPosition = local + new Vector2(18, -18);
            ghostCount.text = "×" + QueueInteraction.DragCount(dragged.Entry.Count, Shift, Control);
            dropBoundary = -1; marker.gameObject.SetActive(false);
            if (!RectTransformUtility.RectangleContainsScreenPoint(queueScroll.viewport, pointer, EventCamera)) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(queueContent, pointer, EventCamera, out var position);
            if (queuedTiles.Count == 0) return;
            var grid = queueContent.GetComponent<GridLayoutGroup>();
            int boundary = QueueInteraction.GridBoundary(queuedTiles.Count, grid.constraintCount, grid.cellSize.x,
                grid.spacing.x, grid.padding.left, position.x - queueContent.rect.xMin, -position.y);
            bool before = boundary < queuedTiles.Count;
            var target = queuedTiles[before ? boundary : boundary - 1];
            var rect = (RectTransform)target.transform;
            int index = plugin.Queue.Entries.ToList().IndexOf(target.Entry);
            if (index < 0) return;
            dropBoundary = index + (before ? 0 : 1);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 1); marker.pivot = new Vector2(0.5f, 1);
            marker.sizeDelta = new Vector2(3, rect.rect.height);
            marker.anchoredPosition = new Vector2(rect.localPosition.x + (before ? -rect.rect.width / 2 : rect.rect.width / 2),
                rect.localPosition.y + rect.rect.height / 2);
            marker.gameObject.SetActive(dropBoundary >= 0);
        }
        internal void EndDrag(PointerEventData data)
        {
            if (!dragged) return;
            UpdateDrag(data.position);
            int source = plugin.Queue.Entries.ToList().IndexOf(dragged.Entry);
            int amount = QueueInteraction.DragCount(dragged.Entry.Count, Shift, Control), boundary = dropBoundary;
            CancelDrag();
            if (source >= 0 && boundary >= 0 && amount > 0) Act(() => plugin.Queue.MoveToBoundary(source, boundary, amount));
        }
        private void CancelDrag()
        {
            dragged = null; dropBoundary = -1;
            if (ghost) ghost.gameObject.SetActive(false); if (marker) marker.gameObject.SetActive(false);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            // Parenting does not inherit layers. The HUD camera only renders its UI layer.
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect) => Place(rect, 0, 0, 1, 1);
        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        { rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent); Place(rect, min.x, min.y, max.x, max.y);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.035f, 0.045f, 0.065f, 0.96f); return rect;
        }
        private static void AddVoidMarker(Transform parent, ItemDef item)
        {
            if (!ItemGroups.TryVoidMarkerColor(item, out var color)) return;
            var marker = Rect("Void tier color", parent);
            // Full-width strip at the tile's outer top edge, inside its unchanged bounds.
            Place(marker, 0, 0.96f, 1, 1);
            var image = marker.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        }
        private TextMeshProUGUI Label(Transform parent, string text, float size)
        {
            var rect = Rect("Text", parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.styleSheet = styleSheet; label.richText = true;
            label.fontSize = size; label.enableAutoSizing = false; label.text = text;
            label.color = Color.white; label.raycastTarget = false; label.alignment = TextAlignmentOptions.TopLeft;
            return label;
        }
        private RectTransform Button(Transform parent, string text, Action action)
        {
            var rect = Rect(text, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.16f, 0.19f, 0.24f, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
            var label = Label(rect, text, 17); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
            return rect;
        }
        private TMP_InputField InputField(Transform parent)
        {
            var rect = Rect("Preset name", parent); rect.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("Viewport", rect); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label(viewport, "", 18); Stretch(text.rectTransform);
            var placeholder = Label(viewport, "Preset name", 18); Stretch(placeholder.rectTransform); placeholder.color = Color.gray;
            text.alignment = placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder;
            return input;
        }
        private ScrollRect Scroll(Transform parent, string name, float x0, float y0, float x1, float y1, out RectTransform content)
        {
            var rect = Box(name, parent, new Vector2(x0, y0), new Vector2(x1, y1));
            var scroll = rect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 25;
            var viewport = Rect("Viewport", rect); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
            viewport.offsetMin = new Vector2(0, 16); viewport.offsetMax = new Vector2(-16, 0);
            scroll.verticalScrollbar = Scrollbar(rect, false); scroll.horizontalScrollbar = Scrollbar(rect, true);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.viewport = viewport; scroll.content = content; scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }
        private static Scrollbar Scrollbar(Transform parent, bool horizontal)
        {
            var bar = Rect(horizontal ? "Horizontal scrollbar" : "Vertical scrollbar", parent);
            if (horizontal)
            {
                bar.anchorMin = Vector2.zero; bar.anchorMax = new Vector2(1, 0);
                bar.pivot = new Vector2(0.5f, 0); bar.sizeDelta = new Vector2(-16, 14); bar.anchoredPosition = new Vector2(-8, 0);
            }
            else
            {
                bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one;
                bar.pivot = new Vector2(1, 0.5f); bar.sizeDelta = new Vector2(14, -16); bar.anchoredPosition = new Vector2(0, 8);
            }
            bar.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f);
            var handle = Rect("Handle", bar); Stretch(handle);
            var graphic = handle.gameObject.AddComponent<Image>(); graphic.color = new Color(0.55f, 0.45f, 0.7f);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = graphic;
            scrollbar.direction = horizontal ? UnityEngine.UI.Scrollbar.Direction.LeftToRight : UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }
        private static void Fit(RectTransform content, bool horizontal)
        {
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            if (horizontal) fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            else fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    // Preview tiles are read-only: clicking them cannot enqueue or change a saved stack.
    internal sealed class QueuePreviewIcon : MonoBehaviour, IPointerEnterHandler, IScrollHandler
    {
        private ScoreboardQueueUI owner;
        private ItemDef item;
        private Image image;
        internal void Initialize(ScoreboardQueueUI ui, ItemDef definition, Image graphic)
        { owner = ui; item = definition; image = graphic; Refresh(); }
        internal void Refresh() { if (item) image.sprite = owner.Sprite(item); }
        public void OnPointerEnter(PointerEventData data) { if (item) owner.Describe(item); }
        public void OnScroll(PointerEventData data) { owner.ScrollPreview(data); data.Use(); }
    }

    internal sealed class QueueItemButton : Button, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private ScoreboardQueueUI owner;
        private Image iconImage;
        private TextMeshProUGUI count;
        private bool wasDragged;
        internal ItemDef Item { get; private set; }
        internal QueueEntry Entry { get; private set; }
        internal void Initialize(ScoreboardQueueUI ui, ItemDef item, QueueEntry entry, Image icon, TextMeshProUGUI text)
        { owner = ui; Item = item; Entry = entry; iconImage = icon; count = text; Refresh(); }
        internal void Refresh()
        { iconImage.sprite = owner.Sprite(Item); count.text = "×" + (Entry?.Count ?? owner.Count(Item)); count.color = Entry == null && owner.Count(Item) == 0 ? Color.gray : Color.white; }
        public override void OnPointerEnter(PointerEventData data) { base.OnPointerEnter(data); owner.Describe(Item); }
        public override void OnPointerDown(PointerEventData data) { wasDragged = false; base.OnPointerDown(data); }
        public override void OnPointerClick(PointerEventData data) { if (!wasDragged) owner.Click(this, data.button); wasDragged = false; }
        public override void OnSubmit(BaseEventData data) { owner.Click(this, PointerEventData.InputButton.Left); data.Use(); }
        public override void OnSelect(BaseEventData data) { base.OnSelect(data); if (owner && Item) owner.Describe(Item); }
        public void OnBeginDrag(PointerEventData data) { if (Entry != null) { wasDragged = true; owner.BeginDrag(this, data); } }
        public void OnDrag(PointerEventData data) => owner.UpdateDrag(data.position);
        public void OnEndDrag(PointerEventData data) => owner.EndDrag(data);
        public void OnScroll(PointerEventData data) { owner.Describe(Item); owner.ScrollDescription(data, Entry != null); data.Use(); }
    }
}



