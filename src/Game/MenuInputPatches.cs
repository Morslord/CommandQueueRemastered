using HarmonyLib;
using RoR2;
using RoR2.UI;
using UnityEngine;

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

    [HarmonyPatch(typeof(LocalUser), "get_isUIFocused")]
    internal static class MenuFocusPatch
    {
        private static void Postfix(LocalUser __instance, ref bool __result)
        {
            if (RemasteredPlugin.Instance?.BlocksGameplay(__instance) == true) __result = true;
        }
    }

    [HarmonyPatch(typeof(LocalUser), "GetUIPassthrough")]
    internal static class MenuPassthroughPatch
    {
        private static bool Prefix(LocalUser __instance, ref UIInputPassthrough __result)
        {
            if (RemasteredPlugin.Instance?.BlocksGameplay(__instance) != true) return true;
            __result = null; return false;
        }
    }

    [HarmonyPatch(typeof(PlayerCharacterMasterController), "Update")]
    internal static class MenuBodyInputPatch
    {
        private static readonly System.Reflection.MethodInfo suppressButtons = AccessTools.Method(typeof(PlayerCharacterMasterController), "SuppressAllButtonsUntilReleased");
        private static bool reportedFailure;
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
