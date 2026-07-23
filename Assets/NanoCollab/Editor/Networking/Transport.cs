using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// TCP transport layer with integrated message routing and framing.
    /// Operates in Host mode (TcpListener + peer relay) or Client mode.
    /// Uses non-blocking tcp.Available checks to guarantee zero main-thread freezing in Unity Editor update loops.
    /// </summary>
    public sealed class Transport : IDisposable
    {
        public enum Mode { None, Host, Client }

        private Mode _mode = Mode.None;
        private TcpListener _listener;
        private readonly List<PeerConnection> _peers = new();
        private PeerConnection _hostConn;
        private bool _disposed;

        private readonly Dictionary<MsgType, Action<BinaryReader>> _handlers = new();
        private readonly byte[] _headerBuf = new byte[3];

        public Mode CurrentMode => _mode;
        public int PeerCount => _peers.Count;

        public event Action<PeerConnection> OnPeerConnected;
        public event Action<PeerConnection> OnPeerDisconnected;

        // --- Message Router Integration ---

        public void RegisterHandler(MsgType type, Action<BinaryReader> handler)
        {
            _handlers[type] = handler;
        }

        public void UnregisterHandler(MsgType type)
        {
            _handlers.Remove(type);
        }

        // --- Lifecycle ---

        public void StartHost(int port)
        {
            if (_mode != Mode.None) return;
            _mode = Mode.Host;

            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _listener.Server.Blocking = false;
                Debug.Log($"[NanoCollab] Hosting TCP server on port {port}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NanoCollab] Failed to start host on port {port}: {ex.Message}");
                _mode = Mode.None;
            }
        }

        public void ConnectToHost(IPAddress address, int port)
        {
            if (_mode != Mode.None) return;
            _mode = Mode.Client;

            try
            {
                var tcp = new TcpClient();
                tcp.Connect(address, port);
                tcp.NoDelay = true;
                tcp.SendTimeout = 2000;
                tcp.ReceiveTimeout = 2000;

                _hostConn = new PeerConnection(tcp);
                _peers.Add(_hostConn);
                Debug.Log($"[NanoCollab] Connected TCP to host {address}:{port}");
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[NanoCollab] Failed to connect to host {address}:{port}: {ex.Message}");
                _mode = Mode.None;
            }
        }

        /// <summary>
        /// Polls TCP socket streams non-blockingly using tcp.Available.
        /// Immediately dispatches complete messages to registered handlers.
        /// </summary>
        public void PollAndDispatch()
        {
            if (_disposed) return;

            // Accept new peers (host mode)
            if (_mode == Mode.Host && _listener != null)
            {
                while (_listener.Server.Poll(0, SelectMode.SelectRead))
                {
                    try
                    {
                        var tcp = _listener.AcceptTcpClient();
                        tcp.NoDelay = true;
                        tcp.SendTimeout = 2000;
                        tcp.ReceiveTimeout = 2000;

                        var peer = new PeerConnection(tcp);
                        _peers.Add(peer);
                        OnPeerConnected?.Invoke(peer);
                        Debug.Log($"[NanoCollab] Host accepted TCP peer: {peer.RemoteEndPoint}");
                    }
                    catch (SocketException) { break; }
                }
            }

            // Read from peers
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                var peer = _peers[i];
                if (!peer.IsConnected)
                {
                    Debug.Log($"[NanoCollab] Peer disconnected: {peer.RemoteEndPoint}");
                    _peers.RemoveAt(i);
                    OnPeerDisconnected?.Invoke(peer);
                    peer.Dispose();
                    continue;
                }

                try
                {
                    while (TryReadMessage(peer, out var msgType, out var payload))
                    {
                        // Dispatch to registered handler
                        if (_handlers.TryGetValue(msgType, out var handler))
                        {
                            using var ms = new MemoryStream(payload);
                            using var r = new BinaryReader(ms);
                            try
                            {
                                handler(r);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[NanoCollab] Handler error for {msgType}: {ex.Message}");
                            }
                        }

                        // Host relays to all other peers
                        if (_mode == Mode.Host)
                        {
                            var frame = SerializationExtensions.FrameMessage(msgType, payload);
                            for (int j = 0; j < _peers.Count; j++)
                            {
                                if (j != i && _peers[j].IsConnected)
                                    _peers[j].Send(frame);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NanoCollab] Peer read error from {peer.RemoteEndPoint}: {ex.Message}");
                    _peers.RemoveAt(i);
                    OnPeerDisconnected?.Invoke(peer);
                    peer.Dispose();
                }
            }
        }

        public void Broadcast(MsgType type, byte[] payload)
        {
            var frame = SerializationExtensions.FrameMessage(type, payload);
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                if (_peers[i].IsConnected)
                {
                    _peers[i].Send(frame);
                }
                else
                {
                    var peer = _peers[i];
                    _peers.RemoveAt(i);
                    OnPeerDisconnected?.Invoke(peer);
                    peer.Dispose();
                }
            }
        }

        public void Shutdown()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var peer in _peers)
                peer.Dispose();
            _peers.Clear();
            _hostConn = null;
            try { _listener?.Stop(); } catch { }
            _listener = null;
            _mode = Mode.None;
        }

        // --- Non-Blocking Framing Accumulator ---

        private bool TryReadMessage(PeerConnection peer, out MsgType msgType, out byte[] payload)
        {
            msgType = 0;
            payload = null;

            var tcp = peer.TcpClient;
            if (tcp == null) return false;

            var stream = peer.Stream;
            if (stream == null) return false;

            // Step 1: Read 3-byte header if not already buffered
            if (!peer.HasHeader)
            {
                if (tcp.Available < 3)
                    return false; // Not enough bytes in OS socket buffer yet — return non-blockingly

                int read = stream.Read(_headerBuf, 0, 3);
                if (read < 3) return false;

                ushort len = (ushort)(_headerBuf[0] | (_headerBuf[1] << 8));
                if (len < 1) return false;

                peer.PendingMsgType = (MsgType)_headerBuf[2];
                peer.PendingPayloadLen = len - 1;
                peer.HasHeader = true;
            }

            // Step 2: Read payload once all bytes arrive in OS buffer
            int needed = peer.PendingPayloadLen;
            if (needed > 0)
            {
                if (tcp.Available < needed)
                    return false; // Payload incomplete in OS buffer — wait for next tick without blocking

                payload = new byte[needed];
                int readPayload = ReadExact(stream, payload, 0, needed);
                if (readPayload < needed) return false;
            }
            else
            {
                payload = Array.Empty<byte>();
            }

            msgType = peer.PendingMsgType;
            peer.HasHeader = false; // Reset for next frame
            return true;
        }

        private static int ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int n = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (n == 0) break;
                totalRead += n;
            }
            return totalRead;
        }
    }

    public sealed class PeerConnection : IDisposable
    {
        private TcpClient _tcp;
        private NetworkStream _stream;

        public Guid UserId { get; set; }
        public TcpClient TcpClient => _tcp;
        public NetworkStream Stream => _stream;
        public string RemoteEndPoint { get; private set; }

        // Non-blocking frame accumulator state
        public bool HasHeader { get; set; }
        public MsgType PendingMsgType { get; set; }
        public int PendingPayloadLen { get; set; }

        public bool IsConnected
        {
            get
            {
                try
                {
                    if (_tcp == null || !_tcp.Connected) return false;
                    if (_tcp.Client.Poll(0, SelectMode.SelectRead) && _tcp.Available == 0)
                        return false;
                    return true;
                }
                catch { return false; }
            }
        }

        public PeerConnection(TcpClient tcp)
        {
            _tcp = tcp;
            _stream = tcp.GetStream();
            try { RemoteEndPoint = tcp.Client.RemoteEndPoint?.ToString() ?? "unknown"; }
            catch { RemoteEndPoint = "unknown"; }
        }

        public void Send(byte[] frame)
        {
            try
            {
                _stream?.Write(frame, 0, frame.Length);
                _stream?.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NanoCollab] Send failed to {RemoteEndPoint}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            _stream = null;
            _tcp = null;
        }
    }
}
