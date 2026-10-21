using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CommandQueueRemastered.Integrations
{
    // Change page capacity only while showing this mod. Other mods retain native four-column pages.
    internal static class OptionalCategoryPaging
    {
        private static string loadingMod;
        internal static void Install(Harmony harmony, Assembly assembly, RemasteredPlugin plugin)
        {
            var targets = new List<MethodBase>();
            try
            {
                var controller = assembly.GetType("RiskOfOptions.Components.Panel.ModOptionPanelController", true);
                var scroll = assembly.GetType("RiskOfOptions.Components.Panel.CategoryScrollRect", true);
                var load = AccessTools.Method(controller, "LoadModOptionsFromOptionCollection");
                var categories = AccessTools.PropertySetter(scroll, "Categories");
                var extra = AccessTools.Method(scroll, "FixExtra");
                if (load == null || categories == null || extra == null || AccessTools.Field(scroll, "categoryButtons") == null)
                    throw new NotSupportedException("Unexpected RiskOfOptions category API.");
                targets.Add(load); targets.Add(categories); targets.Add(extra);
                harmony.Patch(load,
                    prefix: new HarmonyMethod(typeof(OptionalCategoryPaging), nameof(BeginLoad)),
                    finalizer: new HarmonyMethod(typeof(OptionalCategoryPaging), nameof(EndLoad)));
                harmony.Patch(categories,
                    prefix: new HarmonyMethod(typeof(OptionalCategoryPaging), nameof(SetOwner)),
                    transpiler: new HarmonyMethod(typeof(OptionalCategoryPaging), nameof(PageCount)));
                harmony.Patch(extra,
                    transpiler: new HarmonyMethod(typeof(OptionalCategoryPaging), nameof(FillerCount)));
                plugin.LogInfo("RiskOfOptions category paging: three tabs per page for Remastered only.");
            }
            catch (Exception exception)
            {
                foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
                plugin.LogError("RiskOfOptions custom paging unavailable; native paging retained", exception);
            }
        }
        private static void BeginLoad(string __0, out string __state) { __state = loadingMod; loadingMod = __0; }
        private static Exception EndLoad(string __state, Exception __exception) { loadingMod = __state; return __exception; }
        private static void SetOwner(Component __instance)
        {
            var state = __instance.GetComponent<RemasteredCategoryLayout>() ?? __instance.gameObject.AddComponent<RemasteredCategoryLayout>();
            state.IsRemastered = loadingMod == RemasteredPlugin.Guid; state.Scroll = __instance as ScrollRect;
            state.Buttons = AccessTools.Field(__instance.GetType(), "categoryButtons");
        }
        private static int Columns(object scroll) => (scroll as Component)?.GetComponent<RemasteredCategoryLayout>()?.IsRemastered == true ? 3 : 4;
        private static float FloatColumns(object scroll) => Columns(scroll);
        private static IEnumerable<CodeInstruction> PageCount(IEnumerable<CodeInstruction> instructions)
            => ReplaceConstant(instructions, true);
        private static IEnumerable<CodeInstruction> FillerCount(IEnumerable<CodeInstruction> instructions)
            => ReplaceConstant(instructions, false);
        private static IEnumerable<CodeInstruction> ReplaceConstant(IEnumerable<CodeInstruction> instructions, bool floating)
        {
            var code = instructions.ToList();
            var matches = code.Where(instruction => floating
                ? instruction.opcode == OpCodes.Ldc_R4 && Equals(instruction.operand, 4f)
                : instruction.opcode == OpCodes.Ldc_I4_4).ToArray();
            if (matches.Length != 1) throw new NotSupportedException("Unexpected RiskOfOptions page-capacity IL.");
            foreach (var instruction in code)
            {
                if (!ReferenceEquals(instruction, matches[0])) { yield return instruction; continue; }
                instruction.opcode = OpCodes.Ldarg_0; instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OptionalCategoryPaging), floating ? nameof(FloatColumns) : nameof(Columns)));
            }
        }
    }
    internal sealed class RemasteredCategoryLayout : MonoBehaviour
    {
        internal bool IsRemastered;
        internal ScrollRect Scroll;
        internal FieldInfo Buttons;
        private void LateUpdate()
        {
            if (!IsRemastered || !Scroll || !Scroll.viewport || Buttons == null) return;
            var buttons = Buttons.GetValue(Scroll) as List<GameObject>;
            if (buttons == null || buttons.Count == 0 || !buttons[0]) return;
            var layout = buttons[0].transform.parent.GetComponent<HorizontalLayoutGroup>();
            float width = (Scroll.viewport.rect.width - (layout ? layout.padding.horizontal + 2 * layout.spacing : 0)) / 3;
            if (width <= 1) return;
            foreach (var button in buttons)
            {
                if (!button) continue;
                var element = button.GetComponent<LayoutElement>();
                if (element && Mathf.Abs(element.preferredWidth - width) > 0.1f) element.preferredWidth = width;
                foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>())
                { label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 24; label.enableWordWrapping = false; }
            }
        }
    }
}
