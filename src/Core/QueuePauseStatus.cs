using System;
using System.Collections.Generic;

namespace CommandQueueRemastered.Core
{
    public static class QueuePauseMask
    {
        public static uint Capture(IReadOnlyList<string> groups, Func<string, bool> isPaused)
        {
            uint mask = 0;
            for (int i = 0; i < groups.Count && i < 32; i++) if (isPaused(groups[i])) mask |= 1u << i;
            return mask;
        }
        public static bool IsPaused(IReadOnlyList<string> groups, string group, uint mask)
        {
            for (int i = 0; i < groups.Count && i < 32; i++) if (groups[i] == group) return (mask & (1u << i)) != 0;
            return false;
        }
    }
    public sealed class RemoteQueuePauseStatus
    {
        private sealed class Status { public object Connection; public uint Mask; public float Expires; }
        private readonly Dictionary<uint, Status> players = new Dictionary<uint, Status>();
        public void Set(uint player, object connection, uint mask, float now)
            => players[player] = new Status { Connection = connection, Mask = mask, Expires = now + 5f };
        public bool IsPaused(uint player, object connection, int group, float now)
            => group >= 0 && group < 32 && players.TryGetValue(player, out var status) &&
                ReferenceEquals(connection, status.Connection) && now <= status.Expires && (status.Mask & (1u << group)) != 0;
        public void Reset() => players.Clear();
    }
}
