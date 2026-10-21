namespace CommandQueueRemastered.Core
{
    public enum LimiterItemCount { PermanentOnly, IncludeTemporary, IncludeEthereal, IncludeBoth }
    public static class LimiterCounting
    {
        public static bool IncludesTemporary(LimiterItemCount mode) => mode == LimiterItemCount.IncludeTemporary || mode == LimiterItemCount.IncludeBoth;
        public static bool IncludesEthereal(LimiterItemCount mode) => mode == LimiterItemCount.IncludeEthereal || mode == LimiterItemCount.IncludeBoth;
        public static long Count(int permanent, int temporary, int ethereal, LimiterItemCount mode) =>
            (long)permanent + (IncludesTemporary(mode) ? temporary : 0) + (IncludesEthereal(mode) ? ethereal : 0);
        public static int PendingAmount(bool temporary, bool ethereal, LimiterItemCount mode) =>
            !temporary && !ethereal ? 1 : (temporary && IncludesTemporary(mode) ? 1 : 0) + (ethereal && IncludesEthereal(mode) ? 1 : 0);
    }
}
