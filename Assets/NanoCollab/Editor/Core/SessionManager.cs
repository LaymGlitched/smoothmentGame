using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NanoCollab
{
    /// <summary>
    /// Central coordinator for NanoCollab.
    /// Manages state machine, deterministic host election by oldest session start time,
    /// subsystem lifecycles, and update ticks.
    /// </summary>
    public sealed class SessionManager : IDisposable
    {
        public enum SessionState
        {
            Idle,
            Discovering,
            Hosting,
            Connected,
        }

        private SessionState _state = SessionState.Idle;
        public SessionState State => _state;

        // Identity
        private readonly Guid   _localId                = Guid.NewGuid();
        private readonly long   _sessionStartTimeTicks  = DateTime.UtcNow.Ticks;
        private readonly string _userName;

        // Subsystems
        private Discovery        _discovery;
        private Transport        _transport;
        private PresenceManager  _presence;
        private Notification     _notification;
        private CameraSync       _cameraSync;
        private TransformSync    _transformSync;
        private SelectionSync    _selectionSync;
        private HierarchySync    _hierarchySync;
        private SceneOverlay     _sceneOverlay;

        public PresenceManager Presence   => _presence;
        public Transport       Transport  => _transport;
        public CameraSync      CameraSync => _cameraSync;

        private ulong _sessionHash;
        private float _discoverStartTime;
        private const float HostPromotionDelay = 2.5f;

        private readonly Dictionary<Guid, DiscoveryPacket> _discoveredPeers = new();

        private float _lastPingTime;
        private const float PingInterval = 5.0f;

        public SessionManager()
        {
            _userName = NanoCollabSettings.instance.DisplayName;

            _transport    = new Transport();
            _presence     = new PresenceManager();
            _notification = new Notification(_presence);
            _sceneOverlay = new SceneOverlay(_presence);

            RegisterHandlers();

            _cameraSync    = new CameraSync(_transport, _presence, _localId);
            _transformSync = new TransformSync(_transport, _presence, _localId);
            _selectionSync = new SelectionSync(_transport, _presence, _localId);
            _hierarchySync = new HierarchySync(_transport);

            Undo.postprocessModifications += _transformSync.OnPostprocessModifications;
            _transport.OnPeerConnected    += OnPeerTcpConnected;
            _transport.OnPeerDisconnected += OnPeerTcpDisconnected;

            SceneView.duringSceneGui += OnSceneGUI;

            CollabWindow.Bind(this);
        }

        private void RegisterHandlers()
        {
            _transport.RegisterHandler(MsgType.UserJoin, OnUserJoinReceived);
            _transport.RegisterHandler(MsgType.UserLeave, OnUserLeaveReceived);
            _transport.RegisterHandler(MsgType.UserList, OnUserListReceived);
            _transport.RegisterHandler(MsgType.Ping, OnPingReceived);
            _transport.RegisterHandler(MsgType.Pong, OnPongReceived);
        }

        public void OnSceneChanged()
        {
            Disconnect();

            if (!NanoCollabSettings.instance.Enabled) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return;

            StartDiscovery(scene.path);
        }

        public void Tick()
        {
            if (!NanoCollabSettings.instance.Enabled) return;

            _discovery?.Tick();

            if (_state == SessionState.Discovering)
            {
                float now = (float)EditorApplication.timeSinceStartup;
                if (now - _discoverStartTime > HostPromotionDelay)
                    CheckHostElection();
            }

            if (_state == SessionState.Hosting || _state == SessionState.Connected)
            {
                _transport.PollAndDispatch();

                _transformSync.Tick();
                _hierarchySync.Tick();

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

        // --- Host Election & Discovery ---

        private void StartDiscovery(string scenePath)
        {
            int port = NanoCollabSettings.instance.Port;
            _sessionHash = Discovery.ComputeSessionHash(scenePath);

            _discovery = new Discovery(_localId, _sessionHash, _userName, port, (ushort)(port + 1), _sessionStartTimeTicks);
            _discovery.OnPeerFound += OnPeerDiscovered;
            _discovery.OnPeerGone  += OnPeerGone;

            _state = SessionState.Discovering;
            _discoverStartTime = (float)EditorApplication.timeSinceStartup;
            _discoveredPeers.Clear();

            Debug.Log($"[NanoCollab] Discovering peers for session {_sessionHash:X16}...");
        }

        private void OnPeerDiscovered(DiscoveryPacket packet)
        {
            _discoveredPeers[packet.UserId] = packet;

            if (_state == SessionState.Discovering)
            {
                // Immediate host election evaluation
                CheckHostElection();
            }
        }

        private void OnPeerGone(Guid userId)
        {
            _discoveredPeers.Remove(userId);
            _presence.RemoveUser(userId);
        }

        /// <summary>
        /// Deterministic host election: the peer with the earliest SessionStartTimeTicks becomes host.
        /// </summary>
        private void CheckHostElection()
        {
            if (_state != SessionState.Discovering) return;

            // Find the peer with the earliest start time among known peers and ourselves
            long oldestStartTime = _sessionStartTimeTicks;
            DiscoveryPacket? hostCandidate = null;

            foreach (var kv in _discoveredPeers)
            {
                if (kv.Value.SessionStartTimeTicks < oldestStartTime)
                {
                    oldestStartTime = kv.Value.SessionStartTimeTicks;
                    hostCandidate = kv.Value;
                }
            }

            if (hostCandidate.HasValue)
            {
                // Another peer is older — connect to them as client
                var target = hostCandidate.Value;
                _transport.ConnectToHost(target.Address, target.HostPort);
                _state = SessionState.Connected;

                var payload = PresenceManager.WriteUserJoin(_localId, _userName, Color.white, _sessionStartTimeTicks);
                _transport.Broadcast(MsgType.UserJoin, payload);

                _hierarchySync.RebuildSnapshot();
                Debug.Log($"[NanoCollab] Connected as client to host {target.UserName} (oldest peer).");
            }
            else
            {
                // We are the oldest peer — promote to host!
                PromoteToHost();
            }
        }

        private void PromoteToHost()
        {
            if (_state != SessionState.Discovering) return;

            int port = NanoCollabSettings.instance.Port;
            _transport.StartHost(port + 1);
            _state = SessionState.Hosting;

            _hierarchySync.RebuildSnapshot();
            Debug.Log("[NanoCollab] Promoted to host (oldest active peer in session).");
        }

        private void OnPeerTcpConnected(PeerConnection peer)
        {
            if (_state == SessionState.Hosting)
            {
                _presence.AddUser(_localId, _userName, _sessionStartTimeTicks);
                var listPayload = _presence.WriteUserList();
                var frame = SerializationExtensions.FrameMessage(MsgType.UserList, listPayload);
                peer.Send(frame);
            }
        }

        private void OnPeerTcpDisconnected(PeerConnection peer)
        {
            if (peer.UserId != Guid.Empty)
            {
                _presence.RemoveUser(peer.UserId);
                _discoveredPeers.Remove(peer.UserId);
            }

            if (_state == SessionState.Connected && _transport.PeerCount == 0)
            {
                Debug.Log("[NanoCollab] Lost connection to host. Re-discovering & electing new host...");
                _transport.Shutdown();
                _transport = new Transport();
                RegisterHandlers();
                RebindSyncModules();

                _state = SessionState.Discovering;
                _discoverStartTime = (float)EditorApplication.timeSinceStartup;
                _discoveredPeers.Clear();
            }
        }

        private void Disconnect()
        {
            _discovery?.Shutdown();
            _discovery = null;

            if (_transport.CurrentMode != Transport.Mode.None)
            {
                var payload = PresenceManager.WriteUserJoin(_localId, _userName, Color.white, _sessionStartTimeTicks);
                try { _transport.Broadcast(MsgType.UserLeave, payload); } catch { }
            }

            _transport.Shutdown();
            _transport = new Transport();
            RegisterHandlers();
            RebindSyncModules();

            _presence.Clear();
            _discoveredPeers.Clear();
            _state = SessionState.Idle;
        }

        private void RebindSyncModules()
        {
            _selectionSync?.Dispose();
            Undo.postprocessModifications -= _transformSync.OnPostprocessModifications;

            _cameraSync    = new CameraSync(_transport, _presence, _localId);
            _transformSync = new TransformSync(_transport, _presence, _localId);
            _selectionSync = new SelectionSync(_transport, _presence, _localId);
            _hierarchySync = new HierarchySync(_transport);

            Undo.postprocessModifications += _transformSync.OnPostprocessModifications;
        }

        // --- Handlers ---

        private void OnUserJoinReceived(BinaryReader r)
        {
            var (id, name, _, startTime) = PresenceManager.ReadUserJoin(r);
            _presence.AddUser(id, name, startTime);
        }

        private void OnUserLeaveReceived(BinaryReader r)
        {
            var (id, _, _, _) = PresenceManager.ReadUserJoin(r);
            _presence.RemoveUser(id);
            _discoveredPeers.Remove(id);
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

            foreach (var kv in _presence.Users)
                _presence.UpdateUser(kv.Key, user => user.LatencyMs = latencyMs);
        }

        private void SendPing()
        {
            using var ms = new MemoryStream(8);
            using var w  = new BinaryWriter(ms);
            w.Write(Stopwatch.GetTimestamp());
            _transport.Broadcast(MsgType.Ping, ms.ToArray());
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            _cameraSync?.OnSceneGUI(sceneView);
            _sceneOverlay?.OnSceneGUI(sceneView);
            _notification?.DrawSceneGUI(sceneView);
        }
    }
}
