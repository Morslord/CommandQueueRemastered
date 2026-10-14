using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CommandQueueRemastered.Core;
using CommandQueueRemastered.Game;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace CommandQueueRemastered.Integrations
{
    internal sealed class ProperSaveAdapter : IDisposable
    {
        private const string SaveKey = "local.CommandQueueRemastered.queue.v1";
        private readonly RemasteredPlugin plugin;
        private readonly List<KeyValuePair<EventInfo, Delegate>> subscriptions = new List<KeyValuePair<EventInfo, Delegate>>();
        private QueueDocument pending;
        private float loadedAt;
        public ProperSaveAdapter(RemasteredPlugin plugin) { this.plugin = plugin; }

        public void Initialize()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate => candidate.GetType("ProperSave.SaveFile") != null);
            if (assembly == null) { plugin.LogInfo("ProperSave not installed; run queue saving disabled."); return; }
            try
            {
                Subscribe(assembly.GetType("ProperSave.SaveFile", true), "OnGatherSaveData", Gather);
                Subscribe(assembly.GetType("ProperSave.Loading", true), "OnLoadingEnded", Loaded);
                Run.onRunDestroyGlobal += OnRunEnd;
                plugin.LogInfo("ProperSave integration registered (singleplayer / host).");
            }
            catch (Exception exception) { Dispose(); plugin.LogError("ProperSave integration could not initialize", exception); }
        }
        private void Subscribe(Type type, string name, Action<object> callback)
        {
            var eventInfo = type.GetEvent(name, BindingFlags.Public | BindingFlags.Static);
            if (eventInfo == null) throw new MissingMemberException(type.FullName, name);
            var parameters = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();
            if (parameters.Length != 1) throw new InvalidOperationException("Unsupported ProperSave event: " + name);
            var argument = Expression.Parameter(parameters[0].ParameterType);
            var body = Expression.Invoke(Expression.Constant(callback), Expression.Convert(argument, typeof(object)));
            var handler = Expression.Lambda(eventInfo.EventHandlerType, body, argument).Compile();
            eventInfo.AddEventHandler(null, handler);
            subscriptions.Add(new KeyValuePair<EventInfo, Delegate>(eventInfo, handler));
        }
        private void Gather(object data)
        {
            try
            {
                if (!NetworkServer.active || !Run.instance) return;
                ((Dictionary<string, object>)data)[SaveKey] = QueueDocument.Capture(plugin.Queue).Encode();
                plugin.LogInfo("ProperSave captured " + plugin.Queue.Entries.Sum(entry => entry.Count) + " queued items.");
            }
            catch (Exception exception) { plugin.LogError("ProperSave queue capture failed", exception); }
        }
        private void Loaded(object save)
        {
            pending = null;
            try
            {
                if (save == null || !NetworkServer.active) return;
                var method = save.GetType().GetMethods().Single(candidate => candidate.Name == "TryGetModdedData" && candidate.IsGenericMethodDefinition && candidate.GetParameters().Length == 1);
                string json = method.MakeGenericMethod(typeof(string)).Invoke(save, new object[] { SaveKey }) as string;
                if (string.IsNullOrEmpty(json)) { plugin.LogInfo("ProperSave file contains no Remastered queue."); return; }
                pending = QueueDocument.Decode(json);
                loadedAt = Time.unscaledTime;
                plugin.LogInfo("ProperSave queue read; waiting for the local player to spawn.");
            }
            catch (Exception exception) { plugin.LogError("ProperSave queue read failed", exception); }
        }
        public void Update()
        {
            if (pending == null) return;
            var local = LocalUserManager.GetFirstLocalUser();
            if (Run.instance && local?.cachedBody && Time.unscaledTime - loadedAt >= 0.75f)
            {
                var document = pending; pending = null;
                try
                {
                    var valid = document.Queue.Where(entry => ItemGroups.Definition(entry.Pickup)).ToList();
                    int skipped = document.Queue.Except(valid).Sum(entry => entry.Count);
                    plugin.Queue.Replace(valid, document.RepeatedTiers);
                    plugin.LogInfo("ProperSave restored " + valid.Sum(entry => entry.Count) + " queued items; skipped " + skipped + " unavailable items.");
                }
                catch (Exception exception) { plugin.LogError("ProperSave queue restore failed", exception); }
            }
            else if (Time.unscaledTime - loadedAt > 30)
            { pending = null; plugin.LogWarning("ProperSave queue restore timed out waiting for a local player."); }
        }
        private void OnRunEnd(Run run) { pending = null; }
        public void Dispose()
        {
            foreach (var subscription in subscriptions)
            {
                try { subscription.Key.RemoveEventHandler(null, subscription.Value); }
                catch (Exception exception) { plugin.LogError("ProperSave event cleanup failed", exception); }
            }
            subscriptions.Clear(); pending = null;
            Run.onRunDestroyGlobal -= OnRunEnd;
        }
    }
}
