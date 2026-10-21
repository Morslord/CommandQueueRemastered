using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandQueueRemastered.Core
{
    public enum DropSource { Other, Chest, ExchangePool, Teleporter, DrifterSalvage }
    public enum DrifterSalvageMode { Original, SkillUser, ItemDistribution }
    public static class DrifterSalvageSettings
    {
        public static bool TryMigrate(string value, string previousTarget, out DrifterSalvageMode mode)
        {
            mode = DrifterSalvageMode.Original;
            if (Enum.TryParse(value, true, out DrifterSalvageMode current) && Enum.IsDefined(typeof(DrifterSalvageMode), current))
            { mode = current; return true; }
            if (!bool.TryParse(value, out bool enabled)) return false;
            if (!enabled) return true;
            // The earliest checkbox-only build used normal distribution when enabled.
            if (string.IsNullOrWhiteSpace(previousTarget)) { mode = DrifterSalvageMode.ItemDistribution; return true; }
            if (string.Equals(previousTarget, "SkillUser", StringComparison.OrdinalIgnoreCase))
            { mode = DrifterSalvageMode.SkillUser; return true; }
            if (string.Equals(previousTarget, "ItemDistribution", StringComparison.OrdinalIgnoreCase))
            { mode = DrifterSalvageMode.ItemDistribution; return true; }
            return false;
        }
    }
    public static class DistributionRules
    {
        public static bool Allows(bool enabled, string scene, string excludedScenes, DropSource source,
            bool chests, bool pools, bool teleporter, DrifterSalvageMode drifterSalvage = DrifterSalvageMode.SkillUser)
        {
            if (!enabled || (source == DropSource.Chest && !chests) || (source == DropSource.ExchangePool && !pools) ||
                (source == DropSource.Teleporter && !teleporter) ||
                (source == DropSource.DrifterSalvage && drifterSalvage == DrifterSalvageMode.Original)) return false;
            return !StageExclusions.Parse(excludedScenes).Contains(scene ?? "");
        }
    }

    // Outstanding assignments count until the world pickup is consumed or the transfer orb arrives.
    public sealed class DistributionReservation
    {
        public long Recipient { get; set; }
        public string Tier { get; }
        public DistributionReservation(long recipient, string tier) { Recipient = recipient; Tier = tier; }
    }
    public sealed class DistributionLedger
    {
        private readonly HashSet<DistributionReservation> pending = new HashSet<DistributionReservation>();
        public DistributionReservation Reserve(long recipient, string tier)
        { var token = new DistributionReservation(recipient, tier); pending.Add(token); return token; }
        public long Count(long recipient, string tier, InventoryCountMode mode) => pending.LongCount(token =>
            token.Recipient == recipient && (mode == InventoryCountMode.AllItems || token.Tier == tier));
        public void Release(DistributionReservation token) { if (token != null) pending.Remove(token); }
        public void Reset() => pending.Clear();
    }
}
