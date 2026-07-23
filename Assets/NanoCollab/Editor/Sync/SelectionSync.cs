using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Event-driven selection sync using GlobalObjectId.
    /// Broadcasts selection changes on Selection.selectionChanged.
    /// </summary>
    public sealed class SelectionSync
    {
        private readonly Transport       _transport;
        private readonly PresenceManager _presence;
        private readonly Guid            _localId;

        private GlobalObjectId[] _lastSentObjects = Array.Empty<GlobalObjectId>();

        public SelectionSync(Transport transport, PresenceManager presence, Guid localId)
        {
            _transport = transport;
            _presence  = presence;
            _localId   = localId;

            _transport.RegisterHandler(MsgType.SelectionChange, OnSelectionReceived);
            Selection.selectionChanged += OnLocalSelectionChanged;
        }

        public void Dispose()
        {
            Selection.selectionChanged -= OnLocalSelectionChanged;
        }

        private void OnLocalSelectionChanged()
        {
            if (_transport.CurrentMode == Transport.Mode.None) return;

            var gos = Selection.gameObjects;
            var gids = new GlobalObjectId[gos.Length];
            int count = 0;
            for (int i = 0; i < gos.Length; i++)
            {
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(gos[i]);
                if (!gid.Equals(default))
                    gids[count++] = gid;
            }

            Array.Resize(ref gids, count);

            if (gids.SequenceEqual(_lastSentObjects)) return;
            _lastSentObjects = gids;

            using var ms = new MemoryStream(128);
            using var w  = new BinaryWriter(ms);
            w.WriteGuid(_localId);
            w.Write((byte)gids.Length);
            for (int i = 0; i < gids.Length; i++)
                w.WriteGlobalObjectId(gids[i]);

            _transport.Broadcast(MsgType.SelectionChange, ms.ToArray());
        }

        private void OnSelectionReceived(BinaryReader r)
        {
            var userId = r.ReadGuid();
            int count  = r.ReadByte();
            var gids   = new GlobalObjectId[count];
            for (int i = 0; i < count; i++)
                gids[i] = r.ReadGlobalObjectId();

            _presence.UpdateUser(userId, user =>
            {
                user.SelectedObjects = gids;
            });

            SceneView.RepaintAll();
        }
    }
}
