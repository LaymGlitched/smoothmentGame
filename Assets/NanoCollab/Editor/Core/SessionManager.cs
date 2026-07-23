using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NanoCollab
{
    /// <summary>
    /// Central coordinator. Manages the connection lifecycle state machine,
    /// owns all subsystems (discovery, transport, sync modules, presence),
    /// and drives them from EditorApplication.update.
    /// </summary>
    public sealed class SessionManager : IDisposable
    {
        public enum SessionState
        {
            Idle,
            Discovering,
            Hosting,
            Connected, // as client
        }

        private SessionState _state = SessionState.Idle;
        public SessionState State => _state;

        // Identity
        private readonly Guid   _localId = Guid.NewGuid();
        private readonly string _userName;

        // Subsystems
        private Discovery        _discovery;
        private Transport        _transport;
        private MessageRouter    _router;
        private PresenceManager  _presence;
        private Notification     _notification;
        private CameraSync       _cameraSync;
        private TransformSync    _transformSync;
        private SelectionSync    _selectionSync;
        private HierarchySync    _hierarchySync;
        private SceneOverlay     _sceneOverlay;

        // Properties for UI
        public PresenceManager Presence  => _presence;
        public Transport       Transport => _transport;

        // Discovery state
        private ulong  _sessionHash;
        private float  _discoverStartTime;
        private const float HostPromotionDelay = 3.0f; // seconds to wait before becoming host

        // Known peers (to avoid duplicate connections)
        private readonly HashSet<Guid> _knownPeers = new();

        // Ping/pong
        private float _lastPingTime;
        private const float PingInterval = 5.0f;

        public SessionManager()
        {
            _userName = NanoCollabSettings.instance.DisplayName;

            // Create subsystems that exist for the lifetime of the session
            _transport    = new Transport();
            _router       = new MessageRouter();
            _presence     = new PresenceManager();
            _notification = new Notification(_presence);
            _sceneOverlay = new SceneOverlay(_presence);

            // Register presence message handlers
            _router.Register(MsgType.UserJoin, OnUserJoinReceived);
            _router.Register(MsgType.UserLeave, OnUserLeaveReceived);
            _router.Register(MsgType.UserList, OnUserListReceived);
            _router.Register(MsgType.Ping, OnPingReceived);
            _router.Register(MsgType.Pong, OnPongReceived);

            // Create sync modules
            _cameraSync    = new CameraSync(_transport, _presence, _router, _localId);
            _transformSync = new TransformSync(_transport, _router);
            _selectionSync = new SelectionSync(_transport, _presence, _router, _localId);
            _hierarchySync = new HierarchySync(_transport, _router);

            // Hook undo for transform detection
            Undo.postprocessModifications += _transformSync.OnPostprocessModifications;

            // Transport events
            _transport.OnPeerConnected    += OnPeerTcpConnected;
            _transport.OnPeerDisconnected += OnPeerTcpDisconnected;

            // SceneView hooks
            SceneView.duringSceneGui += OnSceneGUI;

            // Bind window
            CollabWindow.Bind(this);
        }

        /// <summary>Called when the active scene changes.</summary>
        public void OnSceneChanged()
        {
            Disconnect();

            if (!NanoCollabSettings.instance.Enabled) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return;

            StartDiscovery(scene.path);
        }

        /// <summary>
        /// Main tick — called from EditorApplication.update.
        /// </summary>
        public void Tick()
        {
            if (!NanoCollabSettings.instance.Enabled) return;

            // Tick discovery
            _discovery?.Tick();

            // Check if we should promote to host
            if (_state == SessionState.Discovering)
            {
                float now = (float)EditorApplication.timeSinceStartup;
                if (now - _discoverStartTime > HostPromotionDelay)
                    PromoteToHost();
            }

            // Poll transport
            if (_state == SessionState.Hosting || _state == SessionState.Connected)
            {
                var messages = _transport.Poll();
                if (messages.Count > 0)
                    _router.Route(messages);

                // Tick sync modules
                _transformSync.Tick();
                _hierarchySync.Tick();

                // Ping/pong
                float now = (float)EditorApplication.timeSinceStartup;
                if (now - _lastPingTime > PingInterval)
                {
                    _lastPingTime = now;
                    SendPing();
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.postprocessModifications -= _transformSync.OnPostprocessModifications;
            _selectionSync?.Dispose();
            CollabWindow.Bind(null);
        }

        // --- Internal State Machine ---

        private void StartDiscovery(string scenePath)
        {
            int port = NanoCollabSettings.instance.Port;
            _sessionHash = Discovery.ComputeSessionHash(Application.dataPath, scenePath);

            _discovery = new Discovery(_localId, _sessionHash, _userName, port, (ushort)(port + 1));
            _discovery.OnPeerFound += OnPeerDiscovered;
            _discovery.OnPeerGone  += OnPeerGone;

            _state = SessionState.Discovering;
            _discoverStartTime = (float)EditorApplication.timeSinceStartup;

            Debug.Log($"[NanoCollab] Discovering peers for session {_sessionHash:X16}...");
        }

        private void PromoteToHost()
        {
            if (_state != SessionState.Discovering) return;

            int port = NanoCollabSettings.instance.Port;
            _transport.StartHost(port + 1);
            _state = SessionState.Hosting;

            // Rebuild hierarchy snapshot for the new session
            _hierarchySync.RebuildSnapshot();

            Debug.Log("[NanoCollab] No peers found — promoted to host.");
        }

        private void OnPeerDiscovered(DiscoveryPacket packet)
        {
            if (_knownPeers.Contains(packet.UserId)) return;
            _knownPeers.Add(packet.UserId);

            if (_state == SessionState.Discovering)
            {
                // Connect to their host port as a client
                _transport.ConnectToHost(packet.Address, packet.HostPort);
                _state = SessionState.Connected;

                // Send our UserJoin
                var payload = PresenceManager.WriteUserJoin(
                    _localId, _userName, new Color(1, 1, 1)); // color assigned by host
                _transport.Broadcast(MsgType.UserJoin, payload);

                _hierarchySync.RebuildSnapshot();

                Debug.Log($"[NanoCollab] Connected to {packet.UserName} at {packet.Address}:{packet.HostPort}");
            }
            else if (_state == SessionState.Hosting)
            {
                // They will connect to us via TCP — nothing to do here
                Debug.Log($"[NanoCollab] Discovered peer {packet.UserName}, waiting for TCP connection.");
            }
        }

        private void OnPeerGone(Guid userId)
        {
            _knownPeers.Remove(userId);
            _presence.RemoveUser(userId);
        }

        private void OnPeerTcpConnected(PeerConnection peer)
        {
            // Host: send current user list to the new peer
            if (_state == SessionState.Hosting)
            {
                // Add ourselves to the list first if not already there
                _presence.AddUser(_localId, _userName);

                var listPayload = _presence.WriteUserList();
                var frame = Serialization.Frame(MsgType.UserList, listPayload);
                peer.Send(frame);
            }
        }

        private void OnPeerTcpDisconnected(PeerConnection peer)
        {
            if (peer.UserId != Guid.Empty)
            {
                _presence.RemoveUser(peer.UserId);
                _knownPeers.Remove(peer.UserId);
            }

            // If we were a client and lost connection to host, go back to discovering
            if (_state == SessionState.Connected && _transport.PeerCount == 0)
            {
                Debug.Log("[NanoCollab] Lost connection to host. Re-discovering...");
                _transport.Shutdown();
                _transport = new Transport();
                // Re-wire transport events
                _transport.OnPeerConnected    += OnPeerTcpConnected;
                _transport.OnPeerDisconnected += OnPeerTcpDisconnected;
                // Re-create sync modules with new transport
                RebindSyncModules();

                _state = SessionState.Discovering;
                _discoverStartTime = (float)EditorApplication.timeSinceStartup;
                _knownPeers.Clear();
            }
        }

        private void Disconnect()
        {
            _discovery?.Shutdown();
            _discovery = null;

            if (_transport.CurrentMode != Transport.Mode.None)
            {
                // Broadcast UserLeave before disconnecting
                var payload = PresenceManager.WriteUserJoin(_localId, _userName, Color.white);
                try { _transport.Broadcast(MsgType.UserLeave, payload); } catch { }
            }

            _transport.Shutdown();
            _transport = new Transport();
            _transport.OnPeerConnected    += OnPeerTcpConnected;
            _transport.OnPeerDisconnected += OnPeerTcpDisconnected;

            RebindSyncModules();

            _presence.Clear();
            _knownPeers.Clear();
            _state = SessionState.Idle;
        }

        private void RebindSyncModules()
        {
            // Re-create sync modules that hold a reference to transport
            _selectionSync?.Dispose();
            Undo.postprocessModifications -= _transformSync.OnPostprocessModifications;

            _router = new MessageRouter();
            _router.Register(MsgType.UserJoin, OnUserJoinReceived);
            _router.Register(MsgType.UserLeave, OnUserLeaveReceived);
            _router.Register(MsgType.UserList, OnUserListReceived);
            _router.Register(MsgType.Ping, OnPingReceived);
            _router.Register(MsgType.Pong, OnPongReceived);

            _cameraSync    = new CameraSync(_transport, _presence, _router, _localId);
            _transformSync = new TransformSync(_transport, _router);
            _selectionSync = new SelectionSync(_transport, _presence, _router, _localId);
            _hierarchySync = new HierarchySync(_transport, _router);

            Undo.postprocessModifications += _transformSync.OnPostprocessModifications;
        }

        // --- Message Handlers ---

        private void OnUserJoinReceived(BinaryReader r)
        {
            var (id, name, _) = PresenceManager.ReadUserJoin(r);
            var user = _presence.AddUser(id, name);

            // Find the peer connection and tag it with the userId
            // (so we can clean up on disconnect)
            // This is a best-effort association
        }

        private void OnUserLeaveReceived(BinaryReader r)
        {
            var (id, _, _) = PresenceManager.ReadUserJoin(r);
            _presence.RemoveUser(id);
            _knownPeers.Remove(id);
        }

        private void OnUserListReceived(BinaryReader r)
        {
            _presence.ReadUserList(r);
        }

        private void OnPingReceived(BinaryReader r)
        {
            long timestamp = r.ReadInt64();
            using var ms = new MemoryStream(8);
            using var w  = new BinaryWriter(ms);
            w.Write(timestamp);
            _transport.Broadcast(MsgType.Pong, ms.ToArray());
        }

        private void OnPongReceived(BinaryReader r)
        {
            long sentTimestamp = r.ReadInt64();
            long now = Stopwatch.GetTimestamp();
            float latencyMs = (float)(now - sentTimestamp) / Stopwatch.Frequency * 1000f;

            // Update latency for all users (simplified — ideally per-peer)
            foreach (var kv in _presence.Users)
            {
                _presence.UpdateUser(kv.Key, user => user.LatencyMs = latencyMs);
            }
        }

        private void SendPing()
        {
            using var ms = new MemoryStream(8);
            using var w  = new BinaryWriter(ms);
            w.Write(Stopwatch.GetTimestamp());
            _transport.Broadcast(MsgType.Ping, ms.ToArray());
        }

        // --- SceneView Callback ---

        private void OnSceneGUI(SceneView sceneView)
        {
            _cameraSync?.OnSceneGUI(sceneView);
            _sceneOverlay?.OnSceneGUI(sceneView);
            _notification?.DrawSceneGUI(sceneView);
        }
    }
}
