using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Detects hierarchy changes (reparent, rename, create, delete) by comparing
    /// a snapshot of the scene hierarchy each tick. Broadcasts changes to peers
    /// and applies remote changes locally.
    /// </summary>
    public sealed class HierarchySync
    {
        private readonly Transport _transport;

        // Snapshot: path → instanceID
        private Dictionary<string, int> _snapshot = new();
        private bool _hierarchyDirty;

        // Suppress applying our own changes back
        private readonly HashSet<string> _suppressPaths = new();
        private float _suppressClearTime;

        public HierarchySync(Transport transport, MessageRouter router)
        {
            _transport = transport;
            router.Register(MsgType.HierarchyChange, OnHierarchyReceived);

            EditorApplication.hierarchyChanged += () => _hierarchyDirty = true;
            RebuildSnapshot();
        }

        /// <summary>
        /// Called from EditorApplication.update. Diffs the hierarchy if it changed.
        /// </summary>
        public void Tick()
        {
            if (_transport.CurrentMode == Transport.Mode.None) return;

            // Clean suppressions
            float now = (float)EditorApplication.timeSinceStartup;
            if (now > _suppressClearTime && _suppressPaths.Count > 0)
                _suppressPaths.Clear();

            if (!_hierarchyDirty) return;
            _hierarchyDirty = false;

            var newSnapshot = BuildSnapshot();
            DiffAndBroadcast(_snapshot, newSnapshot);
            _snapshot = newSnapshot;
        }

        /// <summary>Rebuild the snapshot from scratch (e.g. on scene load).</summary>
        public void RebuildSnapshot()
        {
            _snapshot = BuildSnapshot();
        }

        private Dictionary<string, int> BuildSnapshot()
        {
            var result = new Dictionary<string, int>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
                AddToSnapshot(root.transform, "", result);
            return result;
        }

        private void AddToSnapshot(Transform t, string parentPath, Dictionary<string, int> snapshot)
        {
            string path = parentPath + "/" + t.name;
            snapshot[path] = t.gameObject.GetEntityId();
            for (int i = 0; i < t.childCount; i++)
                AddToSnapshot(t.GetChild(i), path, snapshot);
        }

        private void DiffAndBroadcast(Dictionary<string, int> oldSnap, Dictionary<string, int> newSnap)
        {
            // Detect deletes: in old but not in new
            foreach (var kv in oldSnap)
            {
                if (!newSnap.ContainsKey(kv.Key) && !_suppressPaths.Contains(kv.Key))
                {
                    // Could be delete or rename/reparent
                    // Check if instanceID exists in new snapshot under different path
                    string newPath = null;
                    foreach (var nkv in newSnap)
                    {
                        if (nkv.Value == kv.Value)
                        {
                            newPath = nkv.Key;
                            break;
                        }
                    }

                    if (newPath != null)
                    {
                        // Reparent or rename
                        string oldName = System.IO.Path.GetFileName(kv.Key);
                        string newName = System.IO.Path.GetFileName(newPath);

                        if (oldName != newName)
                            BroadcastChange(HierarchyChangeType.Rename, kv.Key, "", newName, 0);
                        else
                        {
                            // Find sibling index
                            var go = FindByPath(newPath);
                            int sibIndex = go != null ? go.transform.GetSiblingIndex() : 0;
                            string newParent = newPath.Substring(0, newPath.LastIndexOf('/'));
                            BroadcastChange(HierarchyChangeType.Reparent, kv.Key, newParent, "", sibIndex);
                        }
                    }
                    else
                    {
                        BroadcastChange(HierarchyChangeType.Delete, kv.Key, "", "", 0);
                    }
                }
            }

            // Detect creates: in new but not in old (and not a moved object)
            foreach (var kv in newSnap)
            {
                if (!oldSnap.ContainsKey(kv.Key) && !_suppressPaths.Contains(kv.Key))
                {
                    // Check if this instanceID existed in old (moved)
                    bool wasMoved = false;
                    foreach (var okv in oldSnap)
                    {
                        if (okv.Value == kv.Value) { wasMoved = true; break; }
                    }
                    if (!wasMoved)
                    {
                        BroadcastChange(HierarchyChangeType.Create, kv.Key, "", "", 0);
                    }
                }
            }
        }

        private void BroadcastChange(HierarchyChangeType changeType, string objectPath,
                                     string newParentPath, string newName, int siblingIndex)
        {
            using var ms = new MemoryStream(128);
            using var w  = new BinaryWriter(ms);
            w.Write((byte)changeType);
            Serialization.WriteString(w, objectPath);
            Serialization.WriteString(w, newParentPath);
            Serialization.WriteString(w, newName);
            w.Write(siblingIndex);

            _transport.Broadcast(MsgType.HierarchyChange, ms.ToArray());
        }

        private void OnHierarchyReceived(BinaryReader r)
        {
            var changeType    = (HierarchyChangeType)r.ReadByte();
            string objectPath = Serialization.ReadString(r);
            string newParent  = Serialization.ReadString(r);
            string newName    = Serialization.ReadString(r);
            int siblingIndex  = r.ReadInt32();

            // Suppress so we don't echo
            _suppressPaths.Add(objectPath);
            _suppressClearTime = (float)EditorApplication.timeSinceStartup + 0.5f;

            switch (changeType)
            {
                case HierarchyChangeType.Create:
                    CreateObject(objectPath);
                    break;
                case HierarchyChangeType.Delete:
                    DeleteObject(objectPath);
                    break;
                case HierarchyChangeType.Rename:
                    RenameObject(objectPath, newName);
                    break;
                case HierarchyChangeType.Reparent:
                    ReparentObject(objectPath, newParent, siblingIndex);
                    break;
            }

            // Rebuild our snapshot after applying remote changes
            _snapshot = BuildSnapshot();
        }

        private static void CreateObject(string path)
        {
            // Extract name and parent path
            int lastSlash = path.LastIndexOf('/');
            string name = path.Substring(lastSlash + 1);
            string parentPath = lastSlash > 0 ? path.Substring(0, lastSlash) : "";

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "NanoCollab Create");

            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = FindByPath(parentPath);
                if (parent != null)
                    go.transform.SetParent(parent.transform, false);
            }
        }

        private static void DeleteObject(string path)
        {
            var go = FindByPath(path);
            if (go != null)
                Undo.DestroyObjectImmediate(go);
        }

        private static void RenameObject(string path, string newName)
        {
            var go = FindByPath(path);
            if (go != null)
            {
                Undo.RecordObject(go, "NanoCollab Rename");
                go.name = newName;
            }
        }

        private static void ReparentObject(string path, string newParentPath, int siblingIndex)
        {
            var go = FindByPath(path);
            if (go == null) return;

            Transform newParent = null;
            if (!string.IsNullOrEmpty(newParentPath))
            {
                var parentGo = FindByPath(newParentPath);
                if (parentGo != null) newParent = parentGo.transform;
            }

            Undo.SetTransformParent(go.transform, newParent, "NanoCollab Reparent");
            go.transform.SetSiblingIndex(siblingIndex);
        }

        private static GameObject FindByPath(string path)
        {
            // Path format: "/Root/Child/Grandchild"
            // GameObject.Find expects paths without leading slash for root objects
            if (string.IsNullOrEmpty(path)) return null;

            // Try direct find first
            var go = GameObject.Find(path);
            if (go != null) return go;

            // Fallback: strip leading slash
            if (path.StartsWith("/"))
                go = GameObject.Find(path.Substring(1));

            return go;
        }
    }
}
