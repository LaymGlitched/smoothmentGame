using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// UDP broadcast discovery across all local network interfaces (Ethernet, Wi-Fi, ZeroTier).
    /// Announces presence on LAN immediately on scene load/join and periodically (heartbeat every 2s).
    /// Session hash is based on scene NAME (not GUID) so non-git-synced project copies still match.
    /// </summary>
    public sealed class Discovery : IDisposable
    {
        private const uint   Magic           = 0x4E434F4C;
        private const byte   ProtocolVersion = 3; // Bumped: scene-name-based hashing
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

            SendImmediateAnnounce();
        }

        public void SendImmediateAnnounce()
        {
            if (_disposed) return;
            _lastBroadcast = (float)EditorApplication.timeSinceStartup;
            Broadcast(MsgAnnounce);
        }

        public void Tick()
        {
            if (_disposed) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastBroadcast >= HeartbeatInterval)
            {
                _lastBroadcast = now;
                Broadcast(MsgAnnounce);
            }

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

            // 1. Send to global broadcast 255.255.255.255
            SendToEndpoint(bytes, new IPEndPoint(IPAddress.Broadcast, _port));

            // 2. Send to directed broadcast IP of every active IPv4 network interface
            //    (Wi-Fi, Ethernet, ZeroTier, Hamachi, etc.)
            var broadcastAddresses = GetSubnetBroadcastAddresses();
            for (int i = 0; i < broadcastAddresses.Count; i++)
            {
                SendToEndpoint(bytes, new IPEndPoint(broadcastAddresses[i], _port));
            }
        }

        private void SendToEndpoint(byte[] bytes, IPEndPoint ep)
        {
            try
            {
                _udp.Send(bytes, bytes.Length, ep);
            }
            catch (SocketException)
            {
                // Ignored: interface may not support broadcast
            }
        }

        private static List<IPAddress> GetSubnetBroadcastAddresses()
        {
            var list = new List<IPAddress>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var props = ni.GetIPProperties();
                    foreach (var unicast in props.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var ip   = unicast.Address.GetAddressBytes();
                            var mask = unicast.IPv4Mask?.GetAddressBytes();
                            if (mask != null && mask.Length == 4)
                            {
                                var bc = new byte[4];
                                for (int i = 0; i < 4; i++)
                                    bc[i] = (byte)(ip[i] | ~mask[i]);
                                list.Add(new IPAddress(bc));
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
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
            if (sessHash != _sessionHash)
            {
                Debug.Log($"[NanoCollab] Ignoring peer {userName} (session hash mismatch: theirs={sessHash:X16} ours={_sessionHash:X16})");
                return;
            }

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
        /// Computes session hash from the scene FILE NAME (not GUID).
        /// This ensures two developers with independently-created project copies
        /// (e.g., over ZeroTier without shared git) still match sessions
        /// as long as they have the same scene name open.
        /// </summary>
        public static ulong ComputeSessionHash(string scenePath)
        {
            // Use scene file name (e.g., "SampleScene") rather than asset GUID.
            // Asset GUIDs differ between project copies that aren't git-synced.
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (string.IsNullOrEmpty(sceneName)) sceneName = scenePath;

            ulong hash = 14695981039346656037;
            foreach (char c in sceneName)
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
