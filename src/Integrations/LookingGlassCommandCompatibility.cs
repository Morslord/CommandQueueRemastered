using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace CommandQueueRemastered.Integrations
{
    internal static class LookingGlassCommandCompatibility
    {
        private static readonly FieldInfo panelField = AccessTools.Field(typeof(PickupPickerController), "panelInstance");

        internal static void Register(Harmony harmony, RemasteredPlugin plugin)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("LookingGlass.CommandWindowBlur.NoWindowBlur"))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null) return;
            var method = AccessTools.DeclaredMethod(type, "OnDisplayBegin", new[]
            {
                typeof(Action<PickupPickerController, NetworkUIPromptController, LocalUser, CameraRigController>),
                typeof(PickupPickerController), typeof(NetworkUIPromptController), typeof(LocalUser), typeof(CameraRigController)
            });
            if (method == null || method.IsStatic || method.ReturnType != typeof(void) || panelField == null)
            {
                plugin.LogWarning("LookingGlass Command panel API differs; automatic selection compatibility needs verification.");
                return;
            }
            try
            {
                harmony.Patch(method, transpiler: new HarmonyMethod(typeof(LookingGlassCommandCompatibility), nameof(GuardMissingPanel)));
                plugin.LogInfo("LookingGlass Command panel compatibility registered for panel-free queue selection.");
            }
            catch (Exception exception)
            {
                plugin.LogError("LookingGlass Command panel compatibility could not be registered", exception);
            }
        }

        // Preserve orig and all hooks. Only the panel-dependent work is skipped for our pending selection.
        private static bool SkipPanelWork(PickupPickerController picker) => picker &&
            RemasteredPlugin.Instance != null && RemasteredPlugin.Instance.HasPendingSelection(picker) &&
            !(panelField.GetValue(picker) as GameObject);

        private static IEnumerable<CodeInstruction> GuardMissingPanel(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            int panelAccess = codes.FindIndex(code => code.opcode == OpCodes.Ldfld && Equals(code.operand, panelField));
            // Match the inspected instance method: orig.Invoke(...), then self.panelInstance.
            if (panelAccess < 1 || codes[panelAccess - 1].opcode != OpCodes.Ldarg_2 ||
                !codes.Take(panelAccess - 1).Any(code => code.opcode == OpCodes.Callvirt &&
                    code.operand is MethodInfo method && method.Name == "Invoke" &&
                    method.DeclaringType == typeof(Action<PickupPickerController, NetworkUIPromptController, LocalUser, CameraRigController>)))
                throw new InvalidOperationException("LookingGlass Command panel IL differs from the supported layout.");

            int insertion = panelAccess - 1;
            // The bundled Mono Harmony exposes Label from mscorlib; use its list through IList
            // to avoid mixing that reference with the .NET Standard Label facade at compile time.
            object continuePanelWork = typeof(ILGenerator).GetMethod(nameof(ILGenerator.DefineLabel)).Invoke(generator, null);
            var labelsField = typeof(CodeInstruction).GetField("labels");
            var check = new CodeInstruction(OpCodes.Ldarg_2);
            var existingLabels = (IList)labelsField.GetValue(codes[insertion]);
            var checkLabels = (IList)labelsField.GetValue(check);
            foreach (object label in existingLabels) checkLabels.Add(label);
            existingLabels.Clear();
            existingLabels.Add(continuePanelWork);
            codes.InsertRange(insertion, new[]
            {
                check,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LookingGlassCommandCompatibility), nameof(SkipPanelWork))),
                new CodeInstruction(OpCodes.Brfalse, continuePanelWork),
                new CodeInstruction(OpCodes.Ret)
            });
            return codes;
        }
    }
}
