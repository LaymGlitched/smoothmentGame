using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// UDP broadcast discovery. Announces our presence on the LAN and listens
    /// for other NanoCollab editors on the same project + scene.
    /// </summary>
    public sealed class Discovery : IDisposable
    {
        // "NCOL" magic bytes
        private const uint   Magic          = 0x4E434F4C;
        private const byte   ProtocolVersion = 1;
        private const byte   MsgAnnounce    = 0x01;
        private const byte   MsgGoodbye     = 0x02;
        private const float  BroadcastInterval = 2.0f;

        private UdpClient _udp;
        private readonly int    _port;
        private readonly Guid   _localId;
        private readonly ulong  _sessionHash;
        private readonly string _userName;
        private readonly ushort _hostPort;

        private float _lastBroadcast;
        private bool  _disposed;

        /// <summary>Fired when a peer with a matching session is discovered.</summary>
        public event Action<DiscoveryPacket> OnPeerFound;

        /// <summary>Fired when a peer sends a goodbye.</summary>
        public event Action<Guid> OnPeerGone;

        public Discovery(Guid localId, ulong sessionHash, string userName, int port, ushort hostPort)
        {
            _localId     = localId;
            _sessionHash = sessionHash;
            _userName    = userName;
            _port        = port;
            _hostPort    = hostPort;

            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            _udp.EnableBroadcast = true;
            _udp.Client.Blocking = false;

            _lastBroadcast = -BroadcastInterval; // broadcast immediately on first tick
        }

        /// <summary>
        /// Call from EditorApplication.update. Sends periodic announces and
        /// processes any incoming discovery packets. Non-blocking.
        /// </summary>
        public void Tick()
        {
            if (_disposed) return;

            // Periodic broadcast
            float now = (float)UnityEditor.EditorApplication.timeSinceStartup;
            if (now - _lastBroadcast >= BroadcastInterval)
            {
                _lastBroadcast = now;
                Broadcast(MsgAnnounce);
            }

            // Receive
            while (_udp.Available > 0)
            {
                try
                {
                    var ep   = new IPEndPoint(IPAddress.Any, 0);
                    var data = _udp.Receive(ref ep);
                    ProcessPacket(data, ep);
                }
                catch (SocketException)
                {
                    // Non-blocking receive may throw EWOULDBLOCK — safe to ignore
                }
            }
        }

        /// <summary>Broadcast a goodbye and dispose.</summary>
        public void Shutdown()
        {
            if (_disposed) return;
            try { Broadcast(MsgGoodbye); }
            catch { /* best-effort */ }
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
            using var ms = new MemoryStream(64);
            using var w  = new BinaryWriter(ms);

            w.Write(Magic);
            w.Write(ProtocolVersion);
            w.Write(msgType);
            w.Write(_sessionHash);
            Serialization.WriteGuid(w, _localId);
            w.Write(_hostPort);
            Serialization.WriteString(w, _userName);

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
            if (data.Length < 30) return; // minimum packet size

            using var ms = new MemoryStream(data);
            using var r  = new BinaryReader(ms);

            uint magic = r.ReadUInt32();
            if (magic != Magic) return;

            byte version = r.ReadByte();
            if (version != ProtocolVersion) return;

            byte msgType     = r.ReadByte();
            ulong sessHash   = r.ReadUInt64();
            Guid  userId     = Serialization.ReadGuid(r);
            ushort hostPort  = r.ReadUInt16();
            string userName  = Serialization.ReadString(r);

            // Ignore our own packets
            if (userId == _localId) return;

            // Only care about matching sessions
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
                    UserId   = userId,
                    UserName = userName,
                    Address  = sender.Address,
                    HostPort = hostPort,
                });
            }
        }

        // --- Helpers ---

        /// <summary>
        /// Compute a stable session hash from project path and scene path.
        /// </summary>
        public static ulong ComputeSessionHash(string projectPath, string scenePath)
        {
            string combined = projectPath + "|" + scenePath;
            // FNV-1a 64-bit
            ulong hash = 14695981039346656037;
            foreach (char c in combined)
            {
                hash ^= (byte)c;
                hash *= 1099511628211;
            }
            return hash;
        }
    }

    /// <summary>Data received from a discovered peer.</summary>
    public struct DiscoveryPacket
    {
        public Guid      UserId;
        public string    UserName;
        public IPAddress Address;
        public ushort    HostPort;
    }
}
