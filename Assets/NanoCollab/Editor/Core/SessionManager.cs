using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NanoCollab
{
    /// <summary>
    /// Central coordinator for NanoCollab.
    /// Manages state machine, deterministic host election by oldest session start time,
    /// manual direct LAN IP joining, subsystem lifecycles, and update ticks.
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
        private readonly Guid _localId               = Guid.NewGuid();
        private readonly long _sessionStartTimeTicks = DateTime.UtcNow.Ticks;

        public string UserName => NanoCollabSettings.instance.DisplayName;

        // Subsystems
        private Discovery        _discovery;
        private Transport        _transport;
        private PresenceManager  _presence;
        private Notification     _notification;
        private CameraSync       _cameraSync;
        private TransformSync    _transformSync;
        private SelectionSync    _selectionSync;
        private HierarchySync    _hierarchySync;
        private SceneOverlay              _sceneOverlay;
        private SimulatedBot              _bot;
        private CollaboratorAvatarManager _avatarManager;

        public PresenceManager Presence   => _presence;
        public Transport       Transport  => _transport;
        public CameraSync      CameraSync => _cameraSync;
        public SimulatedBot    Bot        => _bot;
        public Guid            LocalId    => _localId;

        private ulong _sessionHash;
        private float _discoverStartTime;
        private const float HostPromotionDelay = 2.5f;

        private float _lastConnectAttemptTime;
        private const float ConnectRetryCooldown = 4.0f;

        private readonly Dictionary<Guid, DiscoveryPacket> _discoveredPeers = new();

        private float _lastPingTime;
        private const float PingInterval = 5.0f;

        private bool _pendingUserJoinBroadcast;

        public SessionManager()
        {
            _transport    = new Transport();
            _presence     = new PresenceManager();
            _notification = new Notification(_presence);
            _sceneOverlay = new SceneOverlay(_presence, _localId);
            _bot          = new SimulatedBot(_presence);
            _avatarManager= new CollaboratorAvatarManager(_presence, _localId);

            RegisterHandlers();
            BindTransportEvents();

            _cameraSync    = new CameraSync(_transport, _presence, _localId);
            _transformSync = new TransformSync(_transport, _presence, _localId);
            _selectionSync = new SelectionSync(_transport, _presence, _localId);
            _hierarchySync = new HierarchySync(_transport);

            Undo.postprocessModifications += _transformSync.OnPostprocessModifications;

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

        private void BindTransportEvents()
        {
            _transport.OnPeerConnected    += OnPeerTcpConnected;
            _transport.OnPeerDisconnected += OnPeerTcpDisconnected;
        }

        public void OnSceneChanged()
        {
            Disconnect();

            if (!NanoCollabSettings.instance.Enabled) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return;

            StartDiscovery(scene.path);
        }

        /// <summary>
        /// Manually initiates non-blocking connection to a host IP address on LAN/ZeroTier.
        /// </summary>
        public void ConnectDirect(string hostIp)
        {
            if (IPAddress.TryParse(hostIp.Trim(), out var ip))
            {
                int port = NanoCollabSettings.instance.Port;
                _transport.ConnectToHost(ip, port + 1);
                _state = SessionState.Discovering;
                Debug.Log($"[NanoCollab] Direct connection initiated to LAN host at {ip}:{port + 1}");
            }
            else
            {
                Debug.LogWarning($"[NanoCollab] Invalid LAN IP address: '{hostIp}'");
            }
        }

        public void Tick()
        {
            if (!NanoCollabSettings.instance.Enabled) return;

            _bot?.Tick();
            _avatarManager?.Tick();
            _discovery?.Tick();

            if (_state == SessionState.Discovering)
            {
                if (_transport.IsConnectedClient)
                {
                    _state = SessionState.Connected;
                    _presence.AddUser(_localId, UserName, _sessionStartTimeTicks, NanoCollabSettings.instance.UserColor);
                    _pendingUserJoinBroadcast = true;
                    Debug.Log("[NanoCollab] Successfully connected to host.");
                }
                else
                {
                    float now = (float)EditorApplication.timeSinceStartup;
                    if (now - _discoverStartTime > HostPromotionDelay)
                        CheckHostElection();
                }
            }

            if (_state == SessionState.Hosting || _state == SessionState.Connected || _transport.IsConnecting)
            {
                _transport.PollAndDispatch();

                if (_state == SessionState.Connected && _pendingUserJoinBroadcast)
                {
                    _pendingUserJoinBroadcast = false;
                    BroadcastLocalUserJoin();
                }

                if (_state == SessionState.Hosting || _state == SessionState.Connected)
                {
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
        }

        public void Dispose()
        {
            Disconnect();
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.postprocessModifications -= _transformSync.OnPostprocessModifications;
            _selectionSync?.Dispose();
            _avatarManager?.Dispose();
            CollabWindow.Bind(null);
        }

        // --- Host Election & Discovery ---

        private void StartDiscovery(string scenePath)
        {
            int port = NanoCollabSettings.instance.Port;
            _sessionHash = Discovery.ComputeSessionHash(scenePath);

            _discovery = new Discovery(_localId, _sessionHash, UserName, port, (ushort)(port + 1), _sessionStartTimeTicks);
            _discovery.OnPeerFound += OnPeerDiscovered;
            _discovery.OnPeerGone  += OnPeerGone;

            _state = SessionState.Discovering;
            _discoverStartTime = (float)EditorApplication.timeSinceStartup;
            _discoveredPeers.Clear();

            Debug.Log($"[NanoCollab] Discovering peers on LAN for session {_sessionHash:X16} (scene: {System.IO.Path.GetFileNameWithoutExtension(scenePath)})...");
        }

        private void OnPeerDiscovered(DiscoveryPacket packet)
        {
            _discoveredPeers[packet.UserId] = packet;

            if (_state == SessionState.Discovering)
            {
                CheckHostElection();
            }
        }

        private void OnPeerGone(Guid userId)
        {
            _discoveredPeers.Remove(userId);
            _presence.RemoveUser(userId);
        }

        private void CheckHostElection()
        {
            if (_state != SessionState.Discovering) return;
            if (_transport.IsConnecting) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastConnectAttemptTime < ConnectRetryCooldown) return;

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
                var target = hostCandidate.Value;
                _lastConnectAttemptTime = now;
                _transport.ConnectToHost(target.Address, target.HostPort);
                _hierarchySync.RebuildSnapshot();
            }
            else
            {
                PromoteToHost();
            }
        }

        private void PromoteToHost()
        {
            if (_state != SessionState.Discovering) return;

            int port = NanoCollabSettings.instance.Port;
            _transport.StartHost(port + 1);
            _state = SessionState.Hosting;

            _presence.AddUser(_localId, UserName, _sessionStartTimeTicks, NanoCollabSettings.instance.UserColor);

            _hierarchySync.RebuildSnapshot();
            Debug.Log("[NanoCollab] Promoted to host (oldest active peer in LAN session).");
        }

        private void OnPeerTcpConnected(PeerConnection peer)
        {
            Debug.Log($"[NanoCollab] TCP peer connected from {peer.RemoteEndPoint}. Sending user list ({_presence.Users.Count} users).");

            if (_state == SessionState.Hosting)
            {
                _presence.AddUser(_localId, UserName, _sessionStartTimeTicks, NanoCollabSettings.instance.UserColor);

                var listPayload = _presence.WriteUserList();
                var frame = SerializationExtensions.FrameMessage(MsgType.UserList, listPayload);
                peer.Send(frame);
            }
        }

        private void OnPeerTcpDisconnected(PeerConnection peer)
        {
            Debug.Log($"[NanoCollab] TCP peer disconnected: {peer.RemoteEndPoint} (UserId: {peer.UserId})");

            if (peer.UserId != Guid.Empty)
            {
                _presence.RemoveUser(peer.UserId);
                _discoveredPeers.Remove(peer.UserId);
            }

            if (_state == SessionState.Connected && _transport.PeerCount == 0)
            {
                Debug.Log("[NanoCollab] Connection to host lost. Re-discovering LAN peers...");
                ReplaceTransport();
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
                var payload = PresenceManager.WriteUserJoin(_localId, UserName, NanoCollabSettings.instance.UserColor, _sessionStartTimeTicks);
                try { _transport.Broadcast(MsgType.UserLeave, payload); } catch { }
            }

            ReplaceTransport();
            _presence.Clear();
            _discoveredPeers.Clear();
            _state = SessionState.Idle;
        }

        private void ReplaceTransport()
        {
            _transport.Shutdown();
            _transport = new Transport();
            RegisterHandlers();
            BindTransportEvents();
            RebindSyncModules();
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

        public void BroadcastLocalUserJoin()
        {
            var name  = NanoCollabSettings.instance.DisplayName;
            var color = NanoCollabSettings.instance.UserColor;
            _presence.AddUser(_localId, name, _sessionStartTimeTicks, color);
            var payload = PresenceManager.WriteUserJoin(_localId, name, color, _sessionStartTimeTicks);
            _transport.Broadcast(MsgType.UserJoin, payload);
            Debug.Log($"[NanoCollab] Sent UserJoin as '{name}' ({_localId})");
        }

        // --- Handlers ---

        private void OnUserJoinReceived(PeerConnection peer, BinaryReader r)
        {
            try
            {
                var (id, name, color, startTime) = PresenceManager.ReadUserJoin(r);
                if (peer != null) peer.UserId = id;

                _presence.AddUser(id, name, startTime);
                Debug.Log($"[NanoCollab] User joined session: {name} ({id})");

                if (_state == SessionState.Hosting)
                {
                    _presence.AddUser(_localId, UserName, _sessionStartTimeTicks, NanoCollabSettings.instance.UserColor);
                    var listPayload = _presence.WriteUserList();
                    _transport.Broadcast(MsgType.UserList, listPayload);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NanoCollab] Safe OnUserJoinReceived caught stream error: {ex.Message}");
            }
        }

        private void OnUserLeaveReceived(BinaryReader r)
        {
            var (id, name, _, _) = PresenceManager.ReadUserJoin(r);
            _presence.RemoveUser(id);
            _discoveredPeers.Remove(id);
            Debug.Log($"[NanoCollab] User left session: {name} ({id})");
        }

        private void OnUserListReceived(BinaryReader r)
        {
            _presence.ReadUserList(r);
            Debug.Log($"[NanoCollab] Received updated user list from host ({_presence.Users.Count} active users in presence)");
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

            _presence.UpdateAllUserLatencies(latencyMs);
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
