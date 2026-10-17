namespace CommandQueueRemastered.Core
{
    public enum MenuInputMode { KeepNative, Block, Allow }
    public enum GameplayInputBlocking { On, CommandQueueOnly, Off }

    public static class MenuInputPolicy
    {
        public static bool BlocksView(GameplayInputBlocking blocking, bool commandQueueView) =>
            blocking != GameplayInputBlocking.Off && (blocking != GameplayInputBlocking.CommandQueueOnly || commandQueueView);

        public static MenuInputMode Resolve(bool ownScoreboardActive, GameplayInputBlocking blocking, bool commandQueueView,
            bool pauseMenuOpen)
        {
            if (!ownScoreboardActive || pauseMenuOpen) return MenuInputMode.KeepNative;
            // Gameplay mode must not depend on selection, hover, typing or cursor counts.
            return BlocksView(blocking, commandQueueView) ? MenuInputMode.Block : MenuInputMode.Allow;
        }

        // Applied only at gameplay call sites. The UI itself keeps its normal focus.
        public static bool GameplayUiFocused(bool nativeFocus, MenuInputMode mode) =>
            mode == MenuInputMode.KeepNative ? nativeFocus : mode == MenuInputMode.Block;
    }
}
