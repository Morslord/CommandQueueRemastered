namespace CommandQueueRemastered.Core
{
    public static class QueueInteraction
    {
        public static int ClickCount(bool shift) => shift ? 10 : 1;
        public const string ClickHelp = "Left-click: +1  |  Shift-left: +10  |  Ctrl-left: add to front";
        public const string RemoveHelp = "Right-click: -1  |  Shift-right: -10  |  Ctrl-right: remove stack";
        public const string DragHelp = "Drag: whole stack  |  Shift-drag: half  |  Ctrl-drag: one";
        public static int DecreaseCount(int stackCount, bool shift, bool control)
            => control ? stackCount : shift ? System.Math.Min(10, stackCount) : 1;
        // Ctrl has priority when both modifiers are held. A one-item stack has no rounded-down half.
        public static int DragCount(int stackCount, bool shift, bool control)
            => control ? 1 : shift ? stackCount / 2 : stackCount;
        // Pointer coordinates start at the content's top-left, including its padding.
        public static int GridBoundary(int count, int columns, float size, float spacing, float padding, float x, float y)
        {
            if (count <= 0) return 0;
            float stride = size + spacing;
            int row = System.Math.Max(0, (int)System.Math.Floor((y - padding) / stride));
            int column = System.Math.Max(0, System.Math.Min(columns - 1, (int)System.Math.Floor((x - padding) / stride)));
            int tile = row * columns + column;
            if (tile >= count) return count;
            bool after = x >= padding + column * stride + size / 2;
            return tile + (after ? 1 : 0);
        }
    }
}
