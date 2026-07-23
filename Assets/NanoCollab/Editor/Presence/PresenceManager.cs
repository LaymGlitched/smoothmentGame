using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Manages connected user identities, active object drag manipulations, and user palette colors.
    /// </summary>
    public sealed class PresenceManager
    {
        private static readonly Color[] Palette =
        {
            new Color(0.33f, 0.69f, 1.00f), // Sky blue
            new Color(1.00f, 0.47f, 0.33f), // Coral
            new Color(0.40f, 0.87f, 0.47f), // Mint green
            new Color(0.93f, 0.60f, 1.00f), // Lavender
            new Color(1.00f, 0.80f, 0.27f), // Gold
            new Color(0.27f, 0.93f, 0.87f), // Teal
            new Color(1.00f, 0.53f, 0.73f), // Pink
            new Color(0.73f, 0.87f, 0.33f), // Lime
        };

        private readonly Dictionary<Guid, CollabUser> _users = new();
        private int _colorIndex;

        public IReadOnlyDictionary<Guid, CollabUser> Users => _users;

        public event Action<CollabUser> OnUserJoined;
        public event Action<CollabUser> OnUserLeft;

        public CollabUser AddUser(Guid id, string name, long sessionStartTimeTicks = 0)
        {
            if (_users.TryGetValue(id, out var existing))
                return existing;

            var user = new CollabUser(id, name, Palette[_colorIndex % Palette.Length], sessionStartTimeTicks);
            _colorIndex++;
            _users[id] = user;
            OnUserJoined?.Invoke(user);
            return user;
        }

        public void RemoveUser(Guid id)
        {
            if (_users.TryGetValue(id, out var user))
            {
                _users.Remove(id);
                OnUserLeft?.Invoke(user);
            }
        }

        public void UpdateUser(Guid id, Action<CollabUser> mutate)
        {
            if (_users.TryGetValue(id, out var user))
            {
                mutate(user);
                _users[id] = user;
            }
        }

        public bool TryGetUser(Guid id, out CollabUser user)
        {
            return _users.TryGetValue(id, out user);
        }

        public void Clear()
        {
            _users.Clear();
            _colorIndex = 0;
        }

        // --- Serialization Helpers ---

        public static byte[] WriteUserJoin(Guid id, string name, Color color, long sessionStartTimeTicks)
        {
            using var ms = new MemoryStream(80);
            using var w  = new BinaryWriter(ms);
            w.WriteGuid(id);
            w.WriteString(name);
            w.WriteColor(color);
            w.Write(sessionStartTimeTicks);
            return ms.ToArray();
        }

        public static (Guid id, string name, Color color, long startTimeTicks) ReadUserJoin(BinaryReader r)
        {
            var id        = r.ReadGuid();
            var name      = r.ReadString();
            var color     = r.ReadColor();
            var startTime = r.ReadInt64();
            return (id, name, color, startTime);
        }

        public byte[] WriteUserList()
        {
            using var ms = new MemoryStream(256);
            using var w  = new BinaryWriter(ms);
            w.Write((byte)_users.Count);
            foreach (var kv in _users)
            {
                w.WriteGuid(kv.Value.Id);
                w.WriteString(kv.Value.Name);
                w.WriteColor(kv.Value.Color);
                w.Write(kv.Value.SessionStartTimeTicks);
            }
            return ms.ToArray();
        }

        public void ReadUserList(BinaryReader r)
        {
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var id        = r.ReadGuid();
                var name      = r.ReadString();
                var color     = r.ReadColor();
                var startTime = r.ReadInt64();
                if (!_users.ContainsKey(id))
                {
                    var user = new CollabUser(id, name, color, startTime);
                    _users[id] = user;
                    _colorIndex++;
                    OnUserJoined?.Invoke(user);
                }
            }
        }
    }
}
