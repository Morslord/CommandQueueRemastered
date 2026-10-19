using System;
using System.Collections.Generic;
using System.Linq;
using CommandQueueRemastered.Core;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace CommandQueueRemastered.Game
{
    // Optional host-requested pause reporting; no queue contents or inventory are transmitted.
    internal sealed class PausedPlayerSync : IDisposable
    {
        private const short MessageId = 29265;
        private const uint Signature = 0x43515035;
        private readonly RemasteredPlugin plugin;
        private readonly RemoteQueuePauseStatus states = new RemoteQueuePauseStatus();
        private readonly HashSet<NetworkConnection> requestedServers = new HashSet<NetworkConnection>();
        private readonly HashSet<NetworkConnection> reportingClients = new HashSet<NetworkConnection>();
        private readonly Dictionary<NetworkConnection, int> queries = new Dictionary<NetworkConnection, int>();
        private readonly HashSet<NetworkClient> clients = new HashSet<NetworkClient>();
        private readonly NetworkMessageDelegate serverHandler, clientHandler;
        private float nextUpdate, nextHeartbeat, nextQuery;
        private uint previousMask = uint.MaxValue;
        private bool collisionReported;
        internal PausedPlayerSync(RemasteredPlugin plugin)
        { this.plugin = plugin; serverHandler = ReceiveState; clientHandler = ReceiveQuery; }

        internal bool IsPaused(CharacterMaster master, string group)
        {
            if (LocalUserManager.readOnlyLocalUsersList.Any(user => user.cachedMaster == master)) return plugin.Queue.IsGroupPaused(group);
            var user = NetworkUser.readOnlyInstancesList.FirstOrDefault(candidate => candidate.master == master);
            return user && states.IsPaused(user.netId.Value, user.connectionToClient, Array.IndexOf(ItemGroups.Names, group), Time.unscaledTime);
        }
        internal void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.2f;
            if (NetworkServer.active)
            {
                if (!NetworkServer.handlers.TryGetValue(MessageId, out var handler)) NetworkServer.RegisterHandler(MessageId, serverHandler);
                else if (handler != serverHandler) ReportCollision();
            }
            foreach (var client in NetworkClient.allClients.ToArray())
            {
                if (!client.handlers.TryGetValue(MessageId, out var handler))
                { client.RegisterHandler(MessageId, clientHandler); clients.Add(client); }
                else if (handler != clientHandler) ReportCollision();
            }
            var activeServers = NetworkClient.allClients.Where(client => client.isConnected).Select(client => client.connection).ToArray();
            requestedServers.RemoveWhere(connection => !activeServers.Contains(connection));
            if (!NetworkServer.active)
            {
                uint mask = QueuePauseMask.Capture(ItemGroups.Names, plugin.Queue.IsGroupPaused);
                if (mask != previousMask || Time.unscaledTime >= nextHeartbeat)
                {
                    foreach (var connection in requestedServers) SendState(connection, mask);
                    previousMask = mask;
                    nextHeartbeat = Time.unscaledTime + 1f;
                }
                return;
            }
            var remote = NetworkUser.readOnlyInstancesList.Where(user => user.master && user.connectionToClient != null &&
                !LocalUserManager.readOnlyLocalUsersList.Any(local => local.currentNetworkUser == user))
                .Select(user => user.connectionToClient).Distinct().ToArray();
            reportingClients.RemoveWhere(connection => !remote.Contains(connection));
            foreach (var stale in queries.Keys.Where(connection => !remote.Contains(connection)).ToArray()) queries.Remove(stale);
            if (!plugin.ExcludePausedPlayers.Value)
            {
                foreach (var connection in reportingClients) connection.Send(MessageId, new PauseMessage { Kind = 2 });
                reportingClients.Clear(); queries.Clear(); states.Reset();
                return;
            }
            if (Time.unscaledTime < nextQuery || !NetworkServer.handlers.TryGetValue(MessageId, out var current) || current != serverHandler) return;
            nextQuery = Time.unscaledTime + 2f;
            foreach (var connection in remote)
            {
                queries.TryGetValue(connection, out int attempts);
                if (!connection.isReady || reportingClients.Contains(connection) || attempts >= 3) continue;
                queries[connection] = attempts + 1;
                connection.Send(MessageId, new PauseMessage { Kind = 0 });
            }
        }
        private void ReportCollision()
        {
            if (collisionReported) return;
            collisionReported = true;
            plugin.LogWarning("Pause synchronization channel is occupied by another mod; unknown remote pause states remain eligible.");
        }
        private void ReceiveQuery(NetworkMessage message)
        {
            try
            {
                var packet = message.ReadMessage<PauseMessage>();
                if (!packet.Valid || NetworkServer.active) return;
                if (packet.Kind == 2) { requestedServers.Remove(message.conn); return; }
                if (packet.Kind != 0) return;
                requestedServers.Add(message.conn);
                SendState(message.conn, QueuePauseMask.Capture(ItemGroups.Names, plugin.Queue.IsGroupPaused));
            }
            catch (Exception exception) { plugin.LogError("Pause request ignored", exception); }
        }
        private void SendState(NetworkConnection connection, uint mask)
        {
            foreach (var local in LocalUserManager.readOnlyLocalUsersList)
            {
                var user = local.currentNetworkUser;
                if (user && user.master) connection.Send(MessageId, new PauseMessage { Kind = 1, Player = user.netId.Value, Mask = mask });
            }
        }
        private void ReceiveState(NetworkMessage message)
        {
            try
            {
                var packet = message.ReadMessage<PauseMessage>();
                if (!NetworkServer.active || !packet.Valid || packet.Kind != 1) return;
                var user = NetworkUser.readOnlyInstancesList.FirstOrDefault(candidate => candidate.netId.Value == packet.Player);
                // A client may report only the pause status of its own NetworkUser.
                if (!user || user.connectionToClient != message.conn || (packet.Mask >> ItemGroups.Names.Length) != 0) return;
                if (!plugin.ExcludePausedPlayers.Value)
                { message.conn.Send(MessageId, new PauseMessage { Kind = 2 }); return; }
                states.Set(packet.Player, message.conn, packet.Mask, Time.unscaledTime);
                reportingClients.Add(message.conn);
            }
            catch (Exception exception) { plugin.LogError("Invalid pause report ignored", exception); }
        }
        internal void Reset() { states.Reset(); queries.Clear(); reportingClients.Clear(); }
        public void Dispose()
        {
            if (NetworkServer.handlers.TryGetValue(MessageId, out var handler) && handler == serverHandler) NetworkServer.UnregisterHandler(MessageId);
            foreach (var client in clients)
                if (client.handlers.TryGetValue(MessageId, out var registered) && registered == clientHandler) client.UnregisterHandler(MessageId);
            clients.Clear(); requestedServers.Clear(); Reset();
        }
        public sealed class PauseMessage : MessageBase
        {
            public byte Kind;
            public uint Player, Mask;
            public bool Valid;
            public override void Serialize(NetworkWriter writer)
            { writer.Write(Signature); writer.Write((byte)1); writer.Write(Kind); writer.Write(Player); writer.Write(Mask); }
            public override void Deserialize(NetworkReader reader)
            { Valid = reader.ReadUInt32() == Signature && reader.ReadByte() == 1; if (Valid) { Kind = reader.ReadByte(); Player = reader.ReadUInt32(); Mask = reader.ReadUInt32(); } }
        }
    }
}
