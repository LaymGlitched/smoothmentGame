using System;
using System.Collections.Generic;
using System.IO;

namespace NanoCollab
{
    /// <summary>
    /// Routes received messages to registered handler callbacks by MsgType.
    /// Sync modules register themselves here.
    /// </summary>
    public sealed class MessageRouter
    {
        private readonly Dictionary<MsgType, Action<BinaryReader>> _handlers = new();

        /// <summary>Register a handler for a specific message type.</summary>
        public void Register(MsgType type, Action<BinaryReader> handler)
        {
            _handlers[type] = handler;
        }

        /// <summary>Unregister a handler.</summary>
        public void Unregister(MsgType type)
        {
            _handlers.Remove(type);
        }

        /// <summary>
        /// Route a list of received messages to their handlers.
        /// Call this after Transport.Poll().
        /// </summary>
        public void Route(List<ReceivedMessage> messages)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                if (_handlers.TryGetValue(msg.Type, out var handler))
                {
                    using var ms = new MemoryStream(msg.Payload);
                    using var r  = new BinaryReader(ms);
                    try
                    {
                        handler(r);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[NanoCollab] Handler error for {msg.Type}: {ex.Message}");
                    }
                }
            }
        }
    }
}
