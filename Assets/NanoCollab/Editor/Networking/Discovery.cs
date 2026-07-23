using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// UDP broadcast discovery. Announces presence on LAN immediately on scene load/join
    /// and periodically (heartbeat every 2s). Computes stable session hash from Scene GUID.
    /// </summary>
    public sealed class Discovery : IDisposable
    {
        private const uint   Magic           = 0x4E434F4C;
        private const byte   ProtocolVersion = 2;
        private const byte   MsgAnnounce     = 0x01;
        private const byte   MsgGoodbye      = 0x02;
        private const float  HeartbeatInterval = 2.0f;

        private UdpClient _udp;
        private readonly int    _port;
        private readonly Guid   _localId;
        private readonly ulong  _sessionHash;
        private readonly string _userName;
        private readonly ushort _hostPort;
        private readonly long   _sessionStartTimeTicks;

        private float _lastBroadcast;
        private bool  _disposed;

        public event Action<DiscoveryPacket> OnPeerFound;
        public event Action<Guid> OnPeerGone;

        public Discovery(Guid localId, ulong sessionHash, string userName, int port, ushort hostPort, long sessionStartTimeTicks)
        {
            _localId                = localId;
            _sessionHash            = sessionHash;
            _userName               = userName;
            _port                   = port;
            _hostPort               = hostPort;
            _sessionStartTimeTicks = sessionStartTimeTicks;

            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            _udp.EnableBroadcast = true;
            _udp.Client.Blocking = false;

            // Broadcast immediately on creation
            SendImmediateAnnounce();
        }

        /// <summary>Broadcasts an announce packet immediately.</summary>
        public void SendImmediateAnnounce()
        {
            if (_disposed) return;
            _lastBroadcast = (float)EditorApplication.timeSinceStartup;
            Broadcast(MsgAnnounce);
        }

        public void Tick()
        {
            if (_disposed) return;

            // Periodic heartbeat broadcast
            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastBroadcast >= HeartbeatInterval)
            {
                _lastBroadcast = now;
                Broadcast(MsgAnnounce);
            }

            // Non-blocking receive
            while (_udp.Available > 0)
            {
                try
                {
                    var ep   = new IPEndPoint(IPAddress.Any, 0);
                    var data = _udp.Receive(ref ep);
                    ProcessPacket(data, ep);
                }
                catch (SocketException) { }
            }
        }

        public void Shutdown()
        {
            if (_disposed) return;
            try { Broadcast(MsgGoodbye); } catch { }
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _udp?.Close(); } catch { }
            _udp = null;
        }

        // --- Internal ---

        private void Broadcast(byte msgType)
        {
            using var ms = new MemoryStream(96);
            using var w  = new BinaryWriter(ms);

            w.Write(Magic);
            w.Write(ProtocolVersion);
            w.Write(msgType);
            w.Write(_sessionHash);
            w.WriteGuid(_localId);
            w.Write(_hostPort);
            w.Write(_sessionStartTimeTicks);
            w.WriteString(_userName);

            var bytes = ms.ToArray();
            try
            {
                _udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, _port));
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[NanoCollab] Discovery broadcast failed: {ex.Message}");
            }
        }

        private void ProcessPacket(byte[] data, IPEndPoint sender)
        {
            if (data.Length < 38) return;

            using var ms = new MemoryStream(data);
            using var r  = new BinaryReader(ms);

            uint magic = r.ReadUInt32();
            if (magic != Magic) return;

            byte version = r.ReadByte();
            if (version != ProtocolVersion) return;

            byte msgType             = r.ReadByte();
            ulong sessHash           = r.ReadUInt64();
            Guid  userId             = r.ReadGuid();
            ushort hostPort          = r.ReadUInt16();
            long  startTimeTicks     = r.ReadInt64();
            string userName          = r.ReadString();

            if (userId == _localId) return;
            if (sessHash != _sessionHash) return;

            if (msgType == MsgGoodbye)
            {
                OnPeerGone?.Invoke(userId);
                return;
            }

            if (msgType == MsgAnnounce)
            {
                OnPeerFound?.Invoke(new DiscoveryPacket
                {
                    UserId                = userId,
                    UserName              = userName,
                    Address               = sender.Address,
                    HostPort              = hostPort,
                    SessionStartTimeTicks = startTimeTicks
                });
            }
        }

        /// <summary>
        /// Compute a stable session hash based on Scene GUID (survives moving project folder).
        /// </summary>
        public static ulong ComputeSessionHash(string scenePath)
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(sceneGuid)) sceneGuid = scenePath;

            // FNV-1a 64-bit
            ulong hash = 14695981039346656037;
            foreach (char c in sceneGuid)
            {
                hash ^= (byte)c;
                hash *= 1099511628211;
            }
            return hash;
        }
    }

    public struct DiscoveryPacket
    {
        public Guid      UserId;
        public string    UserName;
        public IPAddress Address;
        public ushort    HostPort;
        public long      SessionStartTimeTicks;
    }
}
