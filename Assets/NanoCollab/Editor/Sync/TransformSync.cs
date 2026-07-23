using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Detects local transform changes at 30Hz using GlobalObjectId.
    /// Manages object manipulation presence (drag start/stop) and prevents
    /// local Undo stack flooding during continuous remote drag streams.
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
        private const float RotationThreshold = 0.05f;
        private const float ScaleThreshold    = 0.001f;
        private const float PollInterval      = 1f / 30f; // ~30 Hz

        private readonly Transport       _transport;
        private readonly PresenceManager _presence;
        private readonly Guid            _localId;

        private readonly Dictionary<GlobalObjectId, Snapshot> _snapshots = new();
        private float _lastPollTime;

        // Manipulation state
        private GlobalObjectId _activeDragTarget;
        private float          _lastDragChangeTime;
        private const float    DragTimeout = 0.4f;

        private readonly HashSet<GlobalObjectId> _suppressObjects = new();
        private readonly HashSet<GlobalObjectId> _remoteRecordedUndos = new();
        private readonly Dictionary<GlobalObjectId, float> _suppressExpiry = new();

        public TransformSync(Transport transport, PresenceManager presence, Guid localId)
        {
            _transport = transport;
            _presence  = presence;
            _localId   = localId;

            _transport.RegisterHandler(MsgType.TransformUpdate, OnTransformReceived);
            _transport.RegisterHandler(MsgType.Manipulation, OnManipulationReceived);
        }

        public void Tick()
        {
            if (_transport.CurrentMode == Transport.Mode.None) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastPollTime < PollInterval) return;
            _lastPollTime = now;

            CleanSuppressions(now);

            if (_activeDragTarget.IsValid() && now - _lastDragChangeTime > DragTimeout)
            {
                EndLocalDrag();
            }

            var selection = Selection.transforms;
            if (selection == null || selection.Length == 0) return;

            using var ms = new MemoryStream(256);
            using var w  = new BinaryWriter(ms);

            long countPos = ms.Position;
            w.Write((byte)0);
            int count = 0;

            for (int i = 0; i < selection.Length && count < 255; i++)
            {
                var t = selection[i];
                if (t == null) continue;

                var gid = GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject);
                if (!gid.IsValid()) continue;
                if (_suppressObjects.Contains(gid)) continue;

                var current = new Snapshot
                {
                    Position = t.localPosition,
                    Rotation = t.localRotation,
                    Scale    = t.localScale,
                };

                DirtyFlags flags = DirtyFlags.None;

                if (_snapshots.TryGetValue(gid, out var prev))
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
                    flags = DirtyFlags.Position | DirtyFlags.Rotation | DirtyFlags.Scale;
                }

                if (flags == DirtyFlags.None) continue;

                _snapshots[gid] = current;

                if (!_activeDragTarget.Equals(gid))
                {
                    EndLocalDrag();
                    _activeDragTarget = gid;
                    BroadcastManipulation(true, gid);
                }
                _lastDragChangeTime = now;

                w.WriteGlobalObjectId(gid);
                w.Write((byte)flags);
                if ((flags & DirtyFlags.Position) != 0) w.WriteVector3(current.Position);
                if ((flags & DirtyFlags.Rotation) != 0) w.WriteQuaternion(current.Rotation);
                if ((flags & DirtyFlags.Scale)    != 0) w.WriteVector3(current.Scale);

                count++;
            }

            if (count == 0) return;

            ms.Position = countPos;
            w.Write((byte)count);

            _transport.Broadcast(MsgType.TransformUpdate, ms.ToArray());
        }

        private void EndLocalDrag()
        {
            if (_activeDragTarget.IsValid())
            {
                BroadcastManipulation(false, _activeDragTarget);
                _activeDragTarget = default;
            }
        }

        private void BroadcastManipulation(bool isDragging, GlobalObjectId gid)
        {
            using var ms = new MemoryStream(64);
            using var w  = new BinaryWriter(ms);
            w.WriteGuid(_localId);
            w.Write(isDragging);
            w.WriteGlobalObjectId(gid);
            _transport.Broadcast(MsgType.Manipulation, ms.ToArray());
        }

        public UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            _lastPollTime = 0;
            return modifications;
        }

        private void OnTransformReceived(BinaryReader r)
        {
            int count = r.ReadByte();
            float now = (float)EditorApplication.timeSinceStartup;

            for (int i = 0; i < count; i++)
            {
                var gid   = r.ReadGlobalObjectId();
                var flags = (DirtyFlags)r.ReadByte();

                Vector3    pos   = default;
                Quaternion rot   = default;
                Vector3    scale = default;

                if ((flags & DirtyFlags.Position) != 0) pos   = r.ReadVector3();
                if ((flags & DirtyFlags.Rotation) != 0) rot   = r.ReadQuaternion();
                if ((flags & DirtyFlags.Scale)    != 0) scale = r.ReadVector3();

                var targetObj = gid.ToGameObject();
                if (targetObj == null) continue;

                var t = targetObj.transform;

                _suppressObjects.Add(gid);
                _suppressExpiry[gid] = now + 0.2f;

                if (!_remoteRecordedUndos.Contains(gid))
                {
                    Undo.RecordObject(t, "NanoCollab Remote Drag");
                    _remoteRecordedUndos.Add(gid);
                }

                if ((flags & DirtyFlags.Position) != 0) t.localPosition = pos;
                if ((flags & DirtyFlags.Rotation) != 0) t.localRotation = rot;
                if ((flags & DirtyFlags.Scale)    != 0) t.localScale    = scale;

                _snapshots[gid] = new Snapshot
                {
                    Position = t.localPosition,
                    Rotation = t.localRotation,
                    Scale    = t.localScale,
                };
            }
        }

        private void OnManipulationReceived(BinaryReader r)
        {
            var userId     = r.ReadGuid();
            bool isDragging = r.ReadBoolean();
            var gid        = r.ReadGlobalObjectId();

            _presence.UpdateUser(userId, user =>
            {
                user.DraggingObject = isDragging ? gid : default;
            });

            if (!isDragging)
            {
                _remoteRecordedUndos.Remove(gid);
            }

            SceneView.RepaintAll();
        }

        private void CleanSuppressions(float now)
        {
            var expired = new List<GlobalObjectId>();
            foreach (var kv in _suppressExpiry)
            {
                if (now > kv.Value) expired.Add(kv.Key);
            }
            foreach (var gid in expired)
            {
                _suppressObjects.Remove(gid);
                _suppressExpiry.Remove(gid);
                _remoteRecordedUndos.Remove(gid);
            }
        }
    }
}
