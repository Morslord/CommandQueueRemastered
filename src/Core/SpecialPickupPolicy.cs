namespace CommandQueueRemastered.Core
{
    public enum SpecialPickupBehavior { ManualMenu, UseQueueWithoutConsuming, UseQueueNormally }

    public static class SpecialPickupDistribution
    {
        public static uint IgnoreMask(bool temporary, bool ethereal) => (temporary ? 1u : 0u) | (ethereal ? 2u : 0u);
        public static bool Ignores(uint mask, bool temporary, bool ethereal) =>
            (temporary && (mask & 1u) != 0) || (ethereal && (mask & 2u) != 0);
    }

    public static class SpecialPickupPolicy
    {
        public static SpecialPickupBehavior Resolve(bool temporary, bool ethereal,
            SpecialPickupBehavior temporaryBehavior, SpecialPickupBehavior etherealBehavior)
        {
            if (!temporary && !ethereal) return SpecialPickupBehavior.UseQueueNormally;
            // For combined modifiers, the more conservative configured behavior wins.
            if ((temporary && temporaryBehavior == SpecialPickupBehavior.ManualMenu) ||
                (ethereal && etherealBehavior == SpecialPickupBehavior.ManualMenu)) return SpecialPickupBehavior.ManualMenu;
            if ((temporary && temporaryBehavior == SpecialPickupBehavior.UseQueueWithoutConsuming) ||
                (ethereal && etherealBehavior == SpecialPickupBehavior.UseQueueWithoutConsuming)) return SpecialPickupBehavior.UseQueueWithoutConsuming;
            return SpecialPickupBehavior.UseQueueNormally;
        }

        public static void CompleteSelection(QueueState queue, int head, string tier, SpecialPickupBehavior behavior)
        {
            if (behavior == SpecialPickupBehavior.UseQueueNormally) queue.Consume(head, tier);
        }
    }
}
