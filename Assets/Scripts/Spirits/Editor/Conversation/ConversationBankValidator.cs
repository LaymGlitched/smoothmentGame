using System.Collections.Generic;
using GameCode.Spirits.Conversation.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Conversation
{
    /// <summary>
    /// Menu tool that validates all ConversationAsset objects in the project
    /// for structural errors: missing IDs, orphaned nodes, missing speakers,
    /// empty triggers, dead-end branches, and circular references.
    /// </summary>
    public static class ConversationBankValidator
    {
        [MenuItem("Tools/Spirits/Validate All Conversations")]
        public static void ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:ConversationAsset");
            int errorCount = 0;
            int warningCount = 0;
            int assetCount = guids.Length;

            Debug.Log($"[ConversationValidator] Scanning {assetCount} ConversationAsset(s)...");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ConversationAsset>(path);
                if (asset == null) continue;

                string prefix = $"[{asset.name}]";

                // ── ID Check ──
                if (string.IsNullOrWhiteSpace(asset.Id))
                {
                    Debug.LogError($"{prefix} Missing Conversation ID.", asset);
                    errorCount++;
                }

                // ── Trigger Check ──
                if (asset.Triggers == null || asset.Triggers.Length == 0)
                {
                    Debug.LogWarning($"{prefix} No triggers assigned. Conversation will never activate.", asset);
                    warningCount++;
                }
                else
                {
                    foreach (var trigger in asset.Triggers)
                    {
                        if (string.IsNullOrEmpty(trigger.Value))
                        {
                            Debug.LogWarning($"{prefix} Contains an empty trigger entry.", asset);
                            warningCount++;
                        }
                    }
                }

                // ── Node Checks ──
                if (asset.Nodes == null || asset.Nodes.Length == 0)
                {
                    Debug.LogError($"{prefix} No nodes. Add at least one conversation node.", asset);
                    errorCount++;
                    continue;
                }

                // Root node valid?
                if (asset.RootNodeId < 0 || asset.RootNodeId >= asset.Nodes.Length)
                {
                    Debug.LogError($"{prefix} Root node ID {asset.RootNodeId} is out of range (0..{asset.Nodes.Length - 1}).", asset);
                    errorCount++;
                }

                // Build speaker sets for validation
                var requiredSet = new HashSet<string>();
                var optionalSet = new HashSet<string>();

                if (asset.RequiredSpirits != null)
                    foreach (var s in asset.RequiredSpirits)
                        if (!string.IsNullOrEmpty(s.Value)) requiredSet.Add(s.Value);

                if (asset.OptionalSpirits != null)
                    foreach (var s in asset.OptionalSpirits)
                        if (!string.IsNullOrEmpty(s.Value)) optionalSet.Add(s.Value);

                // Reachability analysis
                var reachable = new HashSet<int>();
                CollectReachable(asset, asset.RootNodeId, reachable, new HashSet<int>());

                int orphanCount = asset.Nodes.Length - reachable.Count;
                if (orphanCount > 0)
                {
                    Debug.LogWarning($"{prefix} {orphanCount} node(s) unreachable from root.", asset);
                    warningCount++;
                }

                // Per-node validation
                bool hasTerminal = false;
                for (int i = 0; i < asset.Nodes.Length; i++)
                {
                    var node = asset.Nodes[i];
                    if (node == null)
                    {
                        Debug.LogError($"{prefix} Node at index {i} is null.", asset);
                        errorCount++;
                        continue;
                    }

                    // Speaker registered?
                    if (!string.IsNullOrEmpty(node.Speaker.Value) &&
                        !requiredSet.Contains(node.Speaker.Value) &&
                        !optionalSet.Contains(node.Speaker.Value))
                    {
                        Debug.LogWarning($"{prefix} Node {i}: speaker '{node.Speaker.Value}' not in Required or Optional Spirits.", asset);
                        warningCount++;
                    }

                    // Line key present?
                    if (string.IsNullOrEmpty(node.LineKey.Value))
                    {
                        Debug.LogWarning($"{prefix} Node {i}: empty Line Key.", asset);
                        warningCount++;
                    }

                    // Next node IDs valid?
                    if (node.NextNodeIds != null)
                    {
                        foreach (int nextId in node.NextNodeIds)
                        {
                            if (nextId < 0 || nextId >= asset.Nodes.Length)
                            {
                                Debug.LogError($"{prefix} Node {i}: NextNodeId {nextId} is out of range.", asset);
                                errorCount++;
                            }
                        }
                    }

                    // Terminal check
                    if (node.IsTerminal)
                        hasTerminal = true;

                    // Weight array length mismatch
                    if (node.NextNodeIds != null && node.NextNodeWeights != null &&
                        node.NextNodeWeights.Length > 0 &&
                        node.NextNodeWeights.Length != node.NextNodeIds.Length)
                    {
                        Debug.LogWarning($"{prefix} Node {i}: NextNodeWeights length ({node.NextNodeWeights.Length}) doesn't match NextNodeIds length ({node.NextNodeIds.Length}).", asset);
                        warningCount++;
                    }

                    // Variant weight array length mismatch
                    if (node.Variants != null && node.VariantWeights != null &&
                        node.VariantWeights.Length > 0 &&
                        node.VariantWeights.Length != node.Variants.Length)
                    {
                        Debug.LogWarning($"{prefix} Node {i}: VariantWeights length ({node.VariantWeights.Length}) doesn't match Variants length ({node.Variants.Length}).", asset);
                        warningCount++;
                    }
                }

                if (!hasTerminal)
                {
                    Debug.LogWarning($"{prefix} No terminal nodes found. Conversation may never end.", asset);
                    warningCount++;
                }

                // Circular reference detection
                if (HasCycles(asset))
                {
                    Debug.LogWarning($"{prefix} Contains circular node references. The runtime system handles this safely but it may indicate a design error.", asset);
                    warningCount++;
                }
            }

            string summary = $"[ConversationValidator] Scan complete. {assetCount} asset(s), {errorCount} error(s), {warningCount} warning(s).";
            if (errorCount > 0)
                Debug.LogError(summary);
            else if (warningCount > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        private static void CollectReachable(ConversationAsset asset, int nodeId, HashSet<int> reachable, HashSet<int> visited)
        {
            if (nodeId < 0 || nodeId >= asset.Nodes.Length || visited.Contains(nodeId))
                return;

            visited.Add(nodeId);
            reachable.Add(nodeId);

            var node = asset.Nodes[nodeId];
            if (node?.NextNodeIds == null) return;

            foreach (int next in node.NextNodeIds)
                CollectReachable(asset, next, reachable, visited);
        }

        private static bool HasCycles(ConversationAsset asset)
        {
            var visiting = new HashSet<int>();
            var visited = new HashSet<int>();

            for (int i = 0; i < asset.Nodes.Length; i++)
            {
                if (DfsCycleCheck(asset, i, visiting, visited))
                    return true;
            }

            return false;
        }

        private static bool DfsCycleCheck(ConversationAsset asset, int nodeId, HashSet<int> visiting, HashSet<int> visited)
        {
            if (nodeId < 0 || nodeId >= asset.Nodes.Length) return false;
            if (visited.Contains(nodeId)) return false;
            if (visiting.Contains(nodeId)) return true; // Cycle detected

            visiting.Add(nodeId);

            var node = asset.Nodes[nodeId];
            if (node?.NextNodeIds != null)
            {
                foreach (int next in node.NextNodeIds)
                {
                    if (DfsCycleCheck(asset, next, visiting, visited))
                        return true;
                }
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
            return false;
        }
    }
}
