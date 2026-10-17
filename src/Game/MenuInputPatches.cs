using HarmonyLib;
using RoR2;
using RoR2.UI;
using UnityEngine;
using CommandQueueRemastered.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace CommandQueueRemastered.Game
{
    [HarmonyPatch]
    internal static class QueueEscapePausePatch
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(PauseManager), "TogglePauseScreen");
            yield return AccessTools.DeclaredMethod(typeof(PauseManager), "OpenMinimalPauseScreen");
            yield return AccessTools.DeclaredMethod(typeof(PauseManager), "ClosePauseScreen");
        }
        // Escape dismisses the scoreboard confirmation without also toggling the pause menu.
        private static bool Prefix() => RemasteredPlugin.Instance?.SuppressPauseShortcut != true;
    }

    [HarmonyPatch]
    internal static class MenuGameplayFocusPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(PlayerCharacterMasterController), "Update");
            yield return AccessTools.DeclaredMethod(typeof(PlayerCharacterMasterController), "CanSendBodyInput");
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var nativeFocus = AccessTools.PropertyGetter(typeof(LocalUser), "isUIFocused");
            var gameplayFocus = AccessTools.Method(typeof(MenuGameplayFocusPatch), nameof(GameplayFocused));
            int replaced = 0;
            var result = new List<CodeInstruction>();
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(nativeFocus))
                {
                    instruction.opcode = OpCodes.Call; instruction.operand = gameplayFocus; replaced++;
                }
                result.Add(instruction);
            }
            if (replaced != 1) throw new InvalidOperationException("Unsupported gameplay focus IL in " + __originalMethod.Name + ": " + replaced);
            return result;
        }
        private static bool GameplayFocused(LocalUser user) => MenuInputPolicy.GameplayUiFocused(
            user != null && user.isUIFocused, RemasteredPlugin.Instance?.InputMode(user) ?? MenuInputMode.KeepNative);
    }

    [HarmonyPatch(typeof(PlayerCharacterMasterController), "Update")]
    internal static class MenuBodyInputPatch
    {
        private static readonly Action<PlayerCharacterMasterController> releaseButtons =
            ButtonBufferAccess.CreateReleaseAction<PlayerCharacterMasterController>();
        private static readonly System.Reflection.MethodInfo suppressButtons = AccessTools.Method(typeof(PlayerCharacterMasterController), "SuppressAllButtonsUntilReleased");
        private static bool reportedFailure;
        private static bool Prepare() => releaseButtons != null;
        internal static void SuppressButtons(PlayerCharacterMasterController controller)
        {
            if (!controller || reportedFailure) return;
            try { suppressButtons.Invoke(controller, null); }
            catch (System.Exception exception)
            {
                reportedFailure = true;
                RemasteredPlugin.Instance?.LogError("Could not suppress buffered gameplay buttons", exception);
            }
        }
        private static void Prefix(PlayerCharacterMasterController __instance)
        {
            var user = __instance.networkUser?.localUser;
            if (RemasteredPlugin.Instance?.InputMode(user) == MenuInputMode.Allow) releaseButtons(__instance);
        }
        private static void Postfix(PlayerCharacterMasterController __instance)
        {
            var user = __instance.networkUser?.localUser;
            if (RemasteredPlugin.Instance?.BlocksGameplay(user) != true) return;
            SuppressButtons(__instance);
            var input = user.cachedBody ? user.cachedBody.inputBank : null;
            if (!input) return;
            input.moveVector = Vector3.zero;
            input.SetRawMoveStates(Vector2.zero);
            input.skill1.PushState(false); input.skill2.PushState(false);
            input.skill3.PushState(false); input.skill4.PushState(false);
            input.interact.PushState(false); input.jump.PushState(false);
            input.sprint.PushState(false); input.rawSprintInput.PushState(false);
            input.activateEquipment.PushState(false); input.activateKeyItem.PushState(false);
            input.ping.PushState(false);
            input.keyItemNextElement.PushState(false); input.keyItemPrevElement.PushState(false);
        }
    }
}
