using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CommandQueueRemastered.Game
{
    internal sealed class ItemIconCache : IDisposable
    {
        private readonly RemasteredPlugin plugin;
        private readonly Dictionary<ItemIndex, AsyncOperationHandle<Sprite>> loads = new Dictionary<ItemIndex, AsyncOperationHandle<Sprite>>();
        private readonly HashSet<ItemIndex> reportedFailures = new HashSet<ItemIndex>();
        public ItemIconCache(RemasteredPlugin plugin) { this.plugin = plugin; }

        public Sprite Get(ItemDef item)
        {
            // Modded items may provide a direct sprite instead of an Addressables reference.
            Sprite direct = item.GetIcon();
            if (direct) return direct;
            if (!loads.TryGetValue(item.itemIndex, out var handle))
            {
                try
                {
                    var reference = item.GetIconReference();
                    if (reference == null || !reference.RuntimeKeyIsValid())
                    {
                        ReportFailure(item, "No valid sprite reference is available.");
                        return null;
                    }
                    // Own this handle, rather than loading/releasing the shared ItemDef reference's handle.
                    handle = Addressables.LoadAssetAsync<Sprite>(reference.RuntimeKey);
                    loads.Add(item.itemIndex, handle);
                }
                catch (Exception exception) { ReportFailure(item, exception.ToString()); return null; }
            }
            if (!handle.IsDone) return null;
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result) return handle.Result;
            ReportFailure(item, handle.OperationException?.ToString() ?? "Sprite load returned no result.");
            return null;
        }

        private void ReportFailure(ItemDef item, string reason)
        {
            if (reportedFailures.Add(item.itemIndex))
                plugin.LogWarning("Could not load item icon for " + item.name + ": " + reason);
        }

        public void Dispose()
        {
            foreach (var handle in loads.Values)
                if (handle.IsValid()) Addressables.Release(handle);
            loads.Clear(); reportedFailures.Clear();
        }
    }
}
