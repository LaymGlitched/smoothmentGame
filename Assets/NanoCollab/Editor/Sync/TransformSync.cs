using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Detects local Transform changes and broadcasts them to peers.
    /// Receives remote transform updates and applies them locally.
    /// Delta-only: only changed properties above threshold are transmitted.
    /// </summary>
    public sealed class TransformSync
    {
        [Flags]
        private enum DirtyFlags : byte
        {
            None     = 0,
            Position = 0x01,
            Rotation = 0x02,
            Scale    = 0x04,
        }

        private struct Snapshot
        {
            public Vector3    Position;
            public Quaternion Rotation;
            public Vector3    Scale;
        }

        private const float PositionThreshold = 0.001f;
        private const float RotationThreshold = 0.05f; // degrees
        private const float ScaleThreshold    = 0.001f;
        private const float PollInterval      = 1f / 10f; // ~10 Hz

        private readonly Transport _transport;
        private readonly Dictionary<string, Snapshot> _snapshots = new();
        private float _lastPollTime;

        // Suppress applying our own changes back
        private readonly HashSet<string> _suppressPaths = new();
        private const float SuppressionDuration = 0.2f;
        private readonly Dictionary<string, float> _suppressExpiry = new();

        public TransformSync(Transport transport, MessageRouter router)
        {
            _transport = transport;
            router.Register(MsgType.TransformUpdate, OnTransformReceived);
        }

        /// <summary>
        /// Called from EditorApplication.update to detect and broadcast changes.
        /// </summary>
        public void Tick()
        {
            if (_transport.CurrentMode == Transport.Mode.None) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastPollTime < PollInterval) return;
            _lastPollTime = now;

            // Clean expired suppressions
            CleanSuppressions(now);

            // Check currently selected transforms for changes (covers drag operations)
            var selection = Selection.transforms;
            if (selection == null || selection.Length == 0) return;

            using var ms = new MemoryStream(256);
            using var w  = new BinaryWriter(ms);

            // Reserve space for count byte
            long countPos = ms.Position;
            w.Write((byte)0);
            int count = 0;

            for (int i = 0; i < selection.Length && count < 255; i++)
            {
                var t = selection[i];
                if (t == null) continue;

                string path = GetHierarchyPath(t.gameObject);
                if (_suppressPaths.Contains(path)) continue;

                var current = new Snapshot
                {
                    Position = t.localPosition,
                    Rotation = t.localRotation,
                    Scale    = t.localScale,
                };

                DirtyFlags flags = DirtyFlags.None;

                if (_snapshots.TryGetValue(path, out var prev))
                {
                    if (Vector3.Distance(current.Position, prev.Position) > PositionThreshold)
                        flags |= DirtyFlags.Position;
                    if (Quaternion.Angle(current.Rotation, prev.Rotation) > RotationThreshold)
                        flags |= DirtyFlags.Rotation;
                    if (Vector3.Distance(current.Scale, prev.Scale) > ScaleThreshold)
                        flags |= DirtyFlags.Scale;
                }
                else
                {
                    // First time seeing this object — send everything
                    flags = DirtyFlags.Position | DirtyFlags.Rotation | DirtyFlags.Scale;
                }

                if (flags == DirtyFlags.None) continue;

                _snapshots[path] = current;

                // Write this transform
                Serialization.WriteString(w, path);
                w.Write((byte)flags);
                if ((flags & DirtyFlags.Position) != 0)
                    Serialization.WriteVector3(w, current.Position);
                if ((flags & DirtyFlags.Rotation) != 0)
                    Serialization.WriteQuaternion(w, current.Rotation);
                if ((flags & DirtyFlags.Scale) != 0)
                    Serialization.WriteVector3(w, current.Scale);

                count++;
            }

            if (count == 0) return;

            // Patch count byte
            ms.Position = countPos;
            w.Write((byte)count);

            _transport.Broadcast(MsgType.TransformUpdate, ms.ToArray());
        }

        /// <summary>
        /// Called when any undo/redo operation modifies transforms.
        /// Hook via Undo.postprocessModifications.
        /// </summary>
        public UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            // Force an immediate poll on next tick
            _lastPollTime = 0;
            return modifications;
        }

        private void OnTransformReceived(BinaryReader r)
        {
            int count = r.ReadByte();
            float now = (float)EditorApplication.timeSinceStartup;

            for (int i = 0; i < count; i++)
            {
                string path = Serialization.ReadString(r);
                var flags   = (DirtyFlags)r.ReadByte();

                Vector3    pos   = default;
                Quaternion rot   = default;
                Vector3    scale = default;

                if ((flags & DirtyFlags.Position) != 0) pos   = Serialization.ReadVector3(r);
                if ((flags & DirtyFlags.Rotation) != 0) rot   = Serialization.ReadQuaternion(r);
                if ((flags & DirtyFlags.Scale)    != 0) scale = Serialization.ReadVector3(r);

                // Find the object by path
                var go = GameObject.Find(path);
                if (go == null) continue;

                var t = go.transform;

                // Suppress this path so we don't echo it back
                _suppressPaths.Add(path);
                _suppressExpiry[path] = now + SuppressionDuration;

                Undo.RecordObject(t, "NanoCollab Sync");

                if ((flags & DirtyFlags.Position) != 0) t.localPosition = pos;
                if ((flags & DirtyFlags.Rotation) != 0) t.localRotation = rot;
                if ((flags & DirtyFlags.Scale)    != 0) t.localScale    = scale;

                // Update our snapshot so we don't re-send
                _snapshots[path] = new Snapshot
                {
                    Position = t.localPosition,
                    Rotation = t.localRotation,
                    Scale    = t.localScale,
                };
            }
        }

        private void CleanSuppressions(float now)
        {
            var expired = new List<string>();
            foreach (var kv in _suppressExpiry)
            {
                if (now > kv.Value)
                    expired.Add(kv.Key);
            }
            foreach (var path in expired)
            {
                _suppressPaths.Remove(path);
                _suppressExpiry.Remove(path);
            }
        }

        /// <summary>
        /// Gets the full hierarchy path of a GameObject (e.g. "/Root/Child/Grandchild").
        /// </summary>
        public static string GetHierarchyPath(GameObject go)
        {
            var path = "/" + go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = "/" + parent.name + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
