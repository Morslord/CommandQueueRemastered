using System;
using System.Collections.Generic;
using System.Linq;
using CommandQueueRemastered.Core;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CommandQueueRemastered.Game
{
    internal static class StageExclusionDialog
    {
        private sealed class Choice
        {
            internal string Name, Group;
            internal string[] Scenes;
            internal int Order;
        }
        private static SimpleDialogBox active;
        private static List<Choice> Choices()
        {
            var scenes = SceneCatalog.allSceneDefs.Where(scene => scene && !string.IsNullOrWhiteSpace(scene.baseSceneName) && !string.IsNullOrWhiteSpace(scene.nameToken) &&
                StageExclusions.IsSelectable(scene.baseSceneName, scene.sceneType == SceneType.Stage || scene.sceneType == SceneType.Intermission ||
                    scene.sceneType == SceneType.TimedIntermission || scene.sceneType == SceneType.UntimedStage) &&
                (scene.shouldIncludeInLogbook || scene.stageOrder > 0 || StageExclusions.Parse(StageExclusions.Defaults).Contains(scene.baseSceneName)));
            var choices = scenes.GroupBy(scene => StageExclusions.DisplayName(Language.GetString(scene.nameToken, "en")), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var scene = group.First();
                    int order = scene.isFinalStage ? 6 : scene.stageOrder > 0 ? scene.stageOrder : 7;
                    string heading = order == 1 ? "First Stages" : order == 2 ? "Second Stages" : order == 3 ? "Third Stages" :
                        order == 4 ? "Fourth Stages" : order == 5 ? "Fifth Stages" : order == 6 ? "Final Stages" : "Hidden Realms / Other Environments";
                    return new Choice { Name = string.IsNullOrWhiteSpace(group.Key) || group.Key == scene.nameToken ? scene.baseSceneName : group.Key,
                        Group = heading, Order = order, Scenes = group.Select(entry => entry.baseSceneName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
                }).ToList();
            if (!choices.Any(choice => choice.Scenes.Contains("bazaar")))
                choices.Add(new Choice { Name = "Bazaar Between Time", Group = "Hidden Realms / Other Environments", Order = 7, Scenes = new[] { "bazaar" } });
            if (!choices.Any(choice => choice.Scenes.Contains("computationalexchange")))
                choices.Add(new Choice { Name = "Computational Exchange", Group = "Hidden Realms / Other Environments", Order = 7, Scenes = new[] { "computationalexchange" } });
            return choices.OrderBy(choice => choice.Order).ThenBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        internal static string Description()
        {
            return "Choose stages where item distribution keeps original drop locations. Check a stage to exclude it; Apply saves the selection, Cancel discards changes. Host setting.\n\n" +
                "Defaults:\n• Bazaar Between Time\n• Computational Exchange\n\nAvailable environments (from the installed game catalog; variants with the same name are combined):\n\n" +
                string.Join("\n\n", Choices().GroupBy(choice => choice.Group).Select(group => group.Key + ":\n• " + string.Join("\n• ", group.Select(choice => choice.Name))));
        }
        internal static void Open(RemasteredPlugin plugin)
        {
            if (active) return;
            try
            {
                var choices = Choices();
                var selected = StageExclusions.Parse(plugin.ExcludedDistributionStages.Value);
                var dialog = SimpleDialogBox.Create(MPEventSystemManager.primaryEventSystem); active = dialog;
                dialog.headerToken = new SimpleDialogBox.TokenParamsPair { token = "Excluded Stages" };
                dialog.descriptionToken = new SimpleDialogBox.TokenParamsPair { token = "" };
                var body = dialog.descriptionLabel.rectTransform;
                var size = body.GetComponent<LayoutElement>() ?? body.gameObject.AddComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = 420; size.preferredWidth = 780;
                var viewport = Rect("Stage selection viewport", body); Fill(viewport);
                viewport.offsetMax = new Vector2(-18, 0);
                viewport.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 0.98f);
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = Rect("Stage selection", viewport);
                content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
                content.offsetMin = content.offsetMax = Vector2.zero;
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(8, 8, 8, 8); layout.spacing = 4;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
                var barRect = Rect("Stage scrollbar", body);
                barRect.anchorMin = new Vector2(1, 0); barRect.anchorMax = Vector2.one; barRect.pivot = new Vector2(1, 0.5f);
                barRect.sizeDelta = new Vector2(14, 0); barRect.anchoredPosition = Vector2.zero;
                barRect.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 1);
                var handle = Rect("Handle", barRect); Fill(handle);
                var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(0.55f, 0.6f, 0.7f, 1);
                var scrollbar = barRect.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                var rows = new List<Action>();
                string previousGroup = null;
                foreach (var choice in choices)
                {
                    if (choice.Group != previousGroup)
                    {
                        var heading = Row(content, choice.Group, dialog.descriptionLabel.font, false);
                        heading.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
                        previousGroup = choice.Group;
                    }
                    var row = Row(content, "", dialog.descriptionLabel.font, true);
                    var label = row.GetComponentInChildren<TextMeshProUGUI>();
                    var image = row.GetComponent<Image>();
                    void Refresh()
                    {
                        bool excluded = StageExclusions.ContainsAll(selected, choice.Scenes);
                        bool partial = !excluded && choice.Scenes.Any(selected.Contains);
                        label.text = (excluded ? "[x] " : partial ? "[-] " : "[ ] ") + "<noparse>" + choice.Name + "</noparse>";
                        image.color = excluded ? new Color(0.19f, 0.32f, 0.23f, 1) : new Color(0.12f, 0.15f, 0.21f, 1);
                    }
                    rows.Add(Refresh); Refresh();
                    row.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        StageExclusions.Set(selected, choice.Scenes, !StageExclusions.ContainsAll(selected, choice.Scenes));
                        foreach (var refresh in rows) refresh();
                    });
                }
                dialog.AddActionButton(() => { selected.Clear(); selected.UnionWith(StageExclusions.Parse(StageExclusions.Defaults)); foreach (var refresh in rows) refresh(); }, "Defaults", false);
                dialog.AddActionButton(() => { selected.Clear(); foreach (var refresh in rows) refresh(); }, "None", false);
                dialog.AddActionButton(() => plugin.ExcludedDistributionStages.Value = StageExclusions.Encode(selected), "Apply", true);
                var cancel = dialog.AddCancelButton("Cancel"); HarmonyLib.AccessTools.Field(typeof(SimpleDialogBox), "defaultChoice").SetValue(dialog, cancel);
                if (MPEventSystemManager.primaryEventSystem) MPEventSystemManager.primaryEventSystem.SetSelectedGameObject(cancel.gameObject);
                dialog.gameObject.AddComponent<StageDialogEscape>().Dialog = dialog;
            }
            catch (Exception exception)
            {
                if (active) UnityEngine.Object.Destroy(active.rootObject);
                plugin.LogError("Stage selection dialog could not be opened; existing exclusions retained", exception);
            }
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer; rect.SetParent(parent, false); return rect;
        }
        private static void Fill(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static RectTransform Row(Transform parent, string text, TMP_FontAsset font, bool button)
        {
            var rect = Rect("Stage " + text, parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            if (button) { var image = rect.gameObject.AddComponent<Image>(); rect.gameObject.AddComponent<Button>().targetGraphic = image; }
            var labelRect = Rect("Label", rect); Fill(labelRect); labelRect.offsetMin = new Vector2(8, 0); labelRect.offsetMax = new Vector2(-8, 0);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text;
            label.fontSize = 18; label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 18;
            label.enableWordWrapping = false; label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
            return rect;
        }
    }
    internal sealed class StageDialogEscape : MonoBehaviour
    {
        internal SimpleDialogBox Dialog;
        private void Update() { if (Dialog && Input.GetKeyDown(KeyCode.Escape)) UnityEngine.Object.Destroy(Dialog.rootObject); }
    }
}
