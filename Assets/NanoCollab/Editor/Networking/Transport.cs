using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// TCP transport layer. Operates in Host mode (TcpListener + N clients)
    /// or Client mode (single TcpClient to host). All I/O is non-blocking.
    /// Host relays all messages to every other connected peer.
    /// </summary>
    public sealed class Transport : IDisposable
    {
        public enum Mode { None, Host, Client }

        private Mode _mode = Mode.None;
        private TcpListener _listener;
        private readonly List<PeerConnection> _peers = new();
        private PeerConnection _hostConn; // only used in Client mode
        private bool _disposed;

        // Reusable read buffer
        private readonly byte[] _headerBuf = new byte[3]; // 2 (length) + 1 (type)

        /// <summary>Current transport mode.</summary>
        public Mode CurrentMode => _mode;

        /// <summary>Number of connected peers.</summary>
        public int PeerCount => _peers.Count;

        /// <summary>Fired when a new peer TCP connection is established (host mode only).</summary>
        public event Action<PeerConnection> OnPeerConnected;

        /// <summary>Fired when a peer TCP connection is lost.</summary>
        public event Action<PeerConnection> OnPeerDisconnected;

        // --- Lifecycle ---

        /// <summary>Start as host, listening on the given port.</summary>
        public void StartHost(int port)
        {
            if (_mode != Mode.None) return;
            _mode = Mode.Host;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _listener.Server.Blocking = false;
            Debug.Log($"[NanoCollab] Hosting on port {port}");
        }

        /// <summary>Connect to a host at the given address and port.</summary>
        public void ConnectToHost(IPAddress address, int port)
        {
            if (_mode != Mode.None) return;
            _mode = Mode.Client;

            try
            {
                var tcp = new TcpClient();
                tcp.Connect(address, port);
                tcp.NoDelay = true;
                tcp.Client.Blocking = false;
                _hostConn = new PeerConnection(tcp);
                _peers.Add(_hostConn);
                Debug.Log($"[NanoCollab] Connected to host {address}:{port}");
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[NanoCollab] Failed to connect to host: {ex.Message}");
                _mode = Mode.None;
            }
        }

        /// <summary>
        /// Call every editor tick. Accepts new connections (host), reads all
        /// available messages, returns them for routing. Non-blocking.
        /// </summary>
        public List<ReceivedMessage> Poll()
        {
            var messages = new List<ReceivedMessage>();
            if (_disposed) return messages;

            // Accept new peers (host mode)
            if (_mode == Mode.Host && _listener != null)
            {
                while (_listener.Server.Poll(0, SelectMode.SelectRead))
                {
                    try
                    {
                        var tcp = _listener.AcceptTcpClient();
                        tcp.NoDelay = true;
                        tcp.Client.Blocking = false;
                        var peer = new PeerConnection(tcp);
                        _peers.Add(peer);
                        OnPeerConnected?.Invoke(peer);
                        Debug.Log($"[NanoCollab] Peer connected: {tcp.Client.RemoteEndPoint}");
                    }
                    catch (SocketException) { break; }
                }
            }

            // Read from all peers
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                var peer = _peers[i];
                if (!peer.IsConnected)
                {
                    _peers.RemoveAt(i);
                    OnPeerDisconnected?.Invoke(peer);
                    peer.Dispose();
                    continue;
                }

                try
                {
                    while (TryReadMessage(peer, out var msgType, out var payload))
                    {
                        messages.Add(new ReceivedMessage
                        {
                            Sender  = peer,
                            Type    = msgType,
                            Payload = payload
                        });

                        // Host relays to other peers
                        if (_mode == Mode.Host)
                        {
                            var frame = Serialization.Frame(msgType, payload);
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
                    Debug.LogWarning($"[NanoCollab] Peer read error: {ex.Message}");
                    _peers.RemoveAt(i);
                    OnPeerDisconnected?.Invoke(peer);
                    peer.Dispose();
                }
            }

            return messages;
        }

        /// <summary>Send a framed message to all connected peers.</summary>
        public void Broadcast(MsgType type, byte[] payload)
        {
            var frame = Serialization.Frame(type, payload);
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                if (_peers[i].IsConnected)
                    _peers[i].Send(frame);
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

        // --- Internal ---

        private bool TryReadMessage(PeerConnection peer, out MsgType msgType, out byte[] payload)
        {
            msgType = 0;
            payload = null;

            var stream = peer.Stream;
            if (stream == null || !stream.DataAvailable)
                return false;

            // Read header: 2 bytes length + 1 byte type
            int read = ReadExact(stream, _headerBuf, 0, 3);
            if (read < 3) return false;

            ushort len = (ushort)(_headerBuf[0] | (_headerBuf[1] << 8));
            msgType = (MsgType)_headerBuf[2];

            int payloadLen = len - 1; // subtract type byte
            if (payloadLen > 0)
            {
                payload = new byte[payloadLen];
                ReadExact(stream, payload, 0, payloadLen);
            }
            else
            {
                payload = Array.Empty<byte>();
            }

            return true;
        }

        private static int ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int n = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (n == 0) break; // connection closed
                totalRead += n;
            }
            return totalRead;
        }
    }

    /// <summary>Wraps a single TCP peer connection.</summary>
    public sealed class PeerConnection : IDisposable
    {
        private TcpClient _tcp;
        private NetworkStream _stream;

        /// <summary>Optional user identity, set after UserJoin handshake.</summary>
        public Guid UserId { get; set; }

        public NetworkStream Stream => _stream;

        public bool IsConnected
        {
            get
            {
                try { return _tcp != null && _tcp.Connected && !(_tcp.Client.Poll(0, SelectMode.SelectRead) && _tcp.Available == 0); }
                catch { return false; }
            }
        }

        public PeerConnection(TcpClient tcp)
        {
            _tcp    = tcp;
            _stream = tcp.GetStream();
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
                Debug.LogWarning($"[NanoCollab] Send failed: {ex.Message}");
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

    /// <summary>A message received from a specific peer.</summary>
    public struct ReceivedMessage
    {
        public PeerConnection Sender;
        public MsgType        Type;
        public byte[]         Payload;
    }
}
