using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Event-driven hierarchy change detection (reparent, rename, create, delete)
    /// using persistent GlobalObjectId.
    /// </summary>
    public sealed class HierarchySync
    {
        private readonly Transport _transport;

        private struct ObjectInfo
        {
            public GlobalObjectId ParentGid;
            public string Name;
            public int SiblingIndex;
        }

        private Dictionary<GlobalObjectId, ObjectInfo> _snapshot = new();
        private bool _hierarchyDirty;

        private readonly HashSet<GlobalObjectId> _suppressObjects = new();
        private float _suppressClearTime;

        public HierarchySync(Transport transport)
        {
            _transport = transport;
            _transport.RegisterHandler(MsgType.HierarchyChange, OnHierarchyReceived);

            EditorApplication.hierarchyChanged += () => _hierarchyDirty = true;
            RebuildSnapshot();
        }

        public void Tick()
        {
            if (_transport.CurrentMode == Transport.Mode.None) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now > _suppressClearTime && _suppressObjects.Count > 0)
                _suppressObjects.Clear();

            if (!_hierarchyDirty) return;
            _hierarchyDirty = false;

            var newSnapshot = BuildSnapshot();
            DiffAndBroadcast(_snapshot, newSnapshot);
            _snapshot = newSnapshot;
        }

        public void RebuildSnapshot()
        {
            _snapshot = BuildSnapshot();
        }

        private Dictionary<GlobalObjectId, ObjectInfo> BuildSnapshot()
        {
            var result = new Dictionary<GlobalObjectId, ObjectInfo>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
                AddToSnapshot(root.transform, default, result);
            return result;
        }

        private void AddToSnapshot(Transform t, GlobalObjectId parentGid, Dictionary<GlobalObjectId, ObjectInfo> snapshot)
        {
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject);
            if (gid.IsValid())
            {
                snapshot[gid] = new ObjectInfo
                {
                    ParentGid    = parentGid,
                    Name         = t.name,
                    SiblingIndex = t.GetSiblingIndex()
                };
            }
            for (int i = 0; i < t.childCount; i++)
                AddToSnapshot(t.GetChild(i), gid, snapshot);
        }

        private void DiffAndBroadcast(Dictionary<GlobalObjectId, ObjectInfo> oldSnap,
                                     Dictionary<GlobalObjectId, ObjectInfo> newSnap)
        {
            foreach (var kv in oldSnap)
            {
                if (!newSnap.ContainsKey(kv.Key) && !_suppressObjects.Contains(kv.Key))
                {
                    BroadcastChange(HierarchyChangeType.Delete, kv.Key, default, "", 0);
                }
            }

            foreach (var kv in newSnap)
            {
                if (_suppressObjects.Contains(kv.Key)) continue;

                if (!oldSnap.TryGetValue(kv.Key, out var oldInfo))
                {
                    BroadcastChange(HierarchyChangeType.Create, kv.Key, kv.Value.ParentGid, kv.Value.Name, kv.Value.SiblingIndex);
                }
                else
                {
                    if (oldInfo.Name != kv.Value.Name)
                    {
                        BroadcastChange(HierarchyChangeType.Rename, kv.Key, default, kv.Value.Name, 0);
                    }
                    if (!oldInfo.ParentGid.Equals(kv.Value.ParentGid))
                    {
                        BroadcastChange(HierarchyChangeType.Reparent, kv.Key, kv.Value.ParentGid, "", kv.Value.SiblingIndex);
                    }
                }
            }
        }

        private void BroadcastChange(HierarchyChangeType changeType, GlobalObjectId targetGid,
                                     GlobalObjectId newParentGid, string newName, int siblingIndex)
        {
            using var ms = new MemoryStream(128);
            using var w  = new BinaryWriter(ms);
            w.Write((byte)changeType);
            w.WriteGlobalObjectId(targetGid);
            w.WriteGlobalObjectId(newParentGid);
            w.WriteString(newName);
            w.Write(siblingIndex);

            _transport.Broadcast(MsgType.HierarchyChange, ms.ToArray());
        }

        private void OnHierarchyReceived(BinaryReader r)
        {
            var changeType   = (HierarchyChangeType)r.ReadByte();
            var targetGid    = r.ReadGlobalObjectId();
            var newParentGid = r.ReadGlobalObjectId();
            string newName   = r.ReadString();
            int siblingIndex = r.ReadInt32();

            _suppressObjects.Add(targetGid);
            _suppressClearTime = (float)EditorApplication.timeSinceStartup + 0.5f;

            switch (changeType)
            {
                case HierarchyChangeType.Create:
                    CreateObject(targetGid, newParentGid, newName);
                    break;
                case HierarchyChangeType.Delete:
                    DeleteObject(targetGid);
                    break;
                case HierarchyChangeType.Rename:
                    RenameObject(targetGid, newName);
                    break;
                case HierarchyChangeType.Reparent:
                    ReparentObject(targetGid, newParentGid, siblingIndex);
                    break;
            }

            _snapshot = BuildSnapshot();
        }

        private static void CreateObject(GlobalObjectId targetGid, GlobalObjectId parentGid, string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "New GameObject" : name);
            Undo.RegisterCreatedObjectUndo(go, "NanoCollab Create");

            if (parentGid.IsValid())
            {
                var parentGo = parentGid.ToGameObject();
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, false);
            }
        }

        private static void DeleteObject(GlobalObjectId targetGid)
        {
            var go = targetGid.ToGameObject();
            if (go != null)
                Undo.DestroyObjectImmediate(go);
        }

        private static void RenameObject(GlobalObjectId targetGid, string newName)
        {
            var go = targetGid.ToGameObject();
            if (go != null)
            {
                Undo.RecordObject(go, "NanoCollab Rename");
                go.name = newName;
            }
        }

        private static void ReparentObject(GlobalObjectId targetGid, GlobalObjectId newParentGid, int siblingIndex)
        {
            var go = targetGid.ToGameObject();
            if (go == null) return;

            Transform newParent = null;
            if (newParentGid.IsValid())
            {
                var parentGo = newParentGid.ToGameObject();
                if (parentGo != null) newParent = parentGo.transform;
            }

            Undo.SetTransformParent(go.transform, newParent, "NanoCollab Reparent");
            go.transform.SetSiblingIndex(siblingIndex);
        }
    }
}
