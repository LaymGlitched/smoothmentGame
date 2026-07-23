using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Broadcasts local object selection changes to peers and receives
    /// remote selection updates. Stores remote selections in PresenceManager
    /// for the SceneOverlay to render highlights.
    /// </summary>
    public sealed class SelectionSync
    {
        private readonly Transport       _transport;
        private readonly PresenceManager _presence;
        private readonly Guid            _localId;

        private string[] _lastSentPaths = Array.Empty<string>();

        public SelectionSync(Transport transport, PresenceManager presence,
                             MessageRouter router, Guid localId)
        {
            _transport = transport;
            _presence  = presence;
            _localId   = localId;

            router.Register(MsgType.SelectionChange, OnSelectionReceived);
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
            var paths = new string[gos.Length];
            for (int i = 0; i < gos.Length; i++)
                paths[i] = TransformSync.GetHierarchyPath(gos[i]);

            // Skip if unchanged
            if (paths.SequenceEqual(_lastSentPaths)) return;
            _lastSentPaths = paths;

            // Serialize
            using var ms = new MemoryStream(128);
            using var w  = new BinaryWriter(ms);
            Serialization.WriteGuid(w, _localId);
            w.Write((byte)paths.Length);
            for (int i = 0; i < paths.Length; i++)
                Serialization.WriteString(w, paths[i]);

            _transport.Broadcast(MsgType.SelectionChange, ms.ToArray());
        }

        private void OnSelectionReceived(BinaryReader r)
        {
            var userId = Serialization.ReadGuid(r);
            int count  = r.ReadByte();
            var paths  = new string[count];
            for (int i = 0; i < count; i++)
                paths[i] = Serialization.ReadString(r);

            _presence.UpdateUser(userId, user =>
            {
                user.SelectedPaths = paths;
            });

            SceneView.RepaintAll();
        }
    }
}
