using GameCode.Spirits.Conversation.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Conversation
{
    /// <summary>
    /// Custom Inspector for ConversationAsset. Provides a rich editing experience
    /// with conversation flow preview, node summary, and validation warnings.
    /// </summary>
    [CustomEditor(typeof(ConversationAsset))]
    public class ConversationAssetInspector : UnityEditor.Editor
    {
        // Section foldout states
        private bool showIdentity = true;
        private bool showTriggering = true;
        private bool showMetadata = true;
        private bool showParticipants = true;
        private bool showScoring = false;
        private bool showFlow = true;
        private bool showExit = false;

        // Cached properties
        private SerializedProperty id;
        private SerializedProperty displayName;
        private SerializedProperty description;
        private SerializedProperty triggers;
        private SerializedProperty entryConditions;
        private SerializedProperty basePriority;
        private SerializedProperty category;
        private SerializedProperty cooldownDuration;
        private SerializedProperty cooldownGroup;
        private SerializedProperty isOneShot;
        private SerializedProperty isInterruptible;
        private SerializedProperty canResumeAfterInterrupt;
        private SerializedProperty requiredSpirits;
        private SerializedProperty optionalSpirits;
        private SerializedProperty baseScore;
        private SerializedProperty contextTags;
        private SerializedProperty nodes;
        private SerializedProperty rootNodeId;
        private SerializedProperty exitConditions;
        private SerializedProperty cancelTriggers;

        private void OnEnable()
        {
            id = serializedObject.FindProperty("id");
            displayName = serializedObject.FindProperty("displayName");
            description = serializedObject.FindProperty("description");
            triggers = serializedObject.FindProperty("triggers");
            entryConditions = serializedObject.FindProperty("entryConditions");
            basePriority = serializedObject.FindProperty("basePriority");
            category = serializedObject.FindProperty("category");
            cooldownDuration = serializedObject.FindProperty("cooldownDuration");
            cooldownGroup = serializedObject.FindProperty("cooldownGroup");
            isOneShot = serializedObject.FindProperty("isOneShot");
            isInterruptible = serializedObject.FindProperty("isInterruptible");
            canResumeAfterInterrupt = serializedObject.FindProperty("canResumeAfterInterrupt");
            requiredSpirits = serializedObject.FindProperty("requiredSpirits");
            optionalSpirits = serializedObject.FindProperty("optionalSpirits");
            baseScore = serializedObject.FindProperty("baseScore");
            contextTags = serializedObject.FindProperty("contextTags");
            nodes = serializedObject.FindProperty("nodes");
            rootNodeId = serializedObject.FindProperty("rootNodeId");
            exitConditions = serializedObject.FindProperty("exitConditions");
            cancelTriggers = serializedObject.FindProperty("cancelTriggers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── Title Bar ──
            DrawTitleBar();

            EditorGUILayout.Space(4);

            // ── Flow Preview ──
            DrawFlowPreview();

            EditorGUILayout.Space(8);

            // ── Sections ──
            showIdentity = DrawSection("Identity", showIdentity, () =>
            {
                EditorGUILayout.PropertyField(id);
                EditorGUILayout.PropertyField(displayName);
                EditorGUILayout.PropertyField(description);
            });

            showTriggering = DrawSection("Triggering", showTriggering, () =>
            {
                EditorGUILayout.PropertyField(triggers, true);
                EditorGUILayout.PropertyField(entryConditions, true);
            });

            showMetadata = DrawSection("Metadata", showMetadata, () =>
            {
                EditorGUILayout.PropertyField(basePriority);
                EditorGUILayout.PropertyField(category);
                EditorGUILayout.PropertyField(cooldownDuration);
                EditorGUILayout.PropertyField(cooldownGroup);
                EditorGUILayout.PropertyField(isOneShot);
                EditorGUILayout.PropertyField(isInterruptible);

                if (isInterruptible.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(canResumeAfterInterrupt);
                    EditorGUI.indentLevel--;
                }
            });

            showParticipants = DrawSection("Participants", showParticipants, () =>
            {
                EditorGUILayout.PropertyField(requiredSpirits, true);
                EditorGUILayout.PropertyField(optionalSpirits, true);
            });

            showScoring = DrawSection("Scoring", showScoring, () =>
            {
                EditorGUILayout.PropertyField(baseScore);
                EditorGUILayout.PropertyField(contextTags, true);
            });

            showFlow = DrawSection("Conversation Flow", showFlow, () =>
            {
                EditorGUILayout.PropertyField(rootNodeId);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(nodes, new GUIContent($"Nodes ({nodes.arraySize})"), true);
            });

            showExit = DrawSection("Exit Conditions", showExit, () =>
            {
                EditorGUILayout.PropertyField(exitConditions, true);
                EditorGUILayout.PropertyField(cancelTriggers, true);
            });

            // ── Validation ──
            DrawValidation();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTitleBar()
        {
            var asset = (ConversationAsset)target;

            EditorGUILayout.BeginHorizontal();

            // Category badge
            string categoryLabel = asset.Category.ToString().ToUpperInvariant();
            Color categoryColor = asset.Category switch
            {
                ConversationCategory.Ambient => new Color(0.4f, 0.7f, 0.9f),
                ConversationCategory.Reaction => new Color(1.0f, 0.6f, 0.2f),
                ConversationCategory.Story => new Color(0.9f, 0.3f, 0.9f),
                ConversationCategory.Bond => new Color(0.3f, 0.9f, 0.5f),
                _ => Color.gray
            };

            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2)
            };

            Rect badgeRect = GUILayoutUtility.GetRect(new GUIContent(categoryLabel), badgeStyle, GUILayout.Width(80));
            EditorGUI.DrawRect(badgeRect, categoryColor);
            GUI.Label(badgeRect, categoryLabel, badgeStyle);

            // Priority badge
            string priorityLabel = asset.BasePriority.ToString();
            GUILayout.Label(priorityLabel, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Node count
            int nodeCount = asset.NodeCount;
            GUILayout.Label($"{nodeCount} nodes", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFlowPreview()
        {
            var asset = (ConversationAsset)target;
            if (asset.Nodes == null || asset.Nodes.Length == 0)
            {
                EditorGUILayout.HelpBox("No nodes authored yet.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Flow Preview", EditorStyles.boldLabel);

            // Walk the linear path and display a compact preview
            var visited = new System.Collections.Generic.HashSet<int>();
            int currentId = asset.RootNodeId;

            while (currentId >= 0 && currentId < asset.Nodes.Length && !visited.Contains(currentId))
            {
                visited.Add(currentId);
                var node = asset.Nodes[currentId];
                if (node == null) break;

                string speaker = node.Speaker.Value ?? "???";
                string lineKey = node.LineKey.Value ?? "(no key)";
                Color color = GetSpeakerColor(speaker);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(8);

                // Color bar
                Rect colorRect = GUILayoutUtility.GetRect(4, 16, GUILayout.Width(4));
                EditorGUI.DrawRect(colorRect, color);

                // Speaker
                var speakerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = color },
                    fontSize = 11
                };
                GUILayout.Label($"{speaker}:", speakerStyle, GUILayout.Width(60));

                // Line key
                GUILayout.Label(lineKey, EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();

                // Follow the first branch (linear preview only)
                if (node.NextNodeIds != null && node.NextNodeIds.Length > 0)
                {
                    currentId = node.NextNodeIds[0];

                    if (node.NextNodeIds.Length > 1)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(76);
                        GUILayout.Label($"↳ branches to [{string.Join(", ", node.NextNodeIds)}]",
                            new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal = { textColor = new Color(0.7f, 0.7f, 0.4f) }
                            });
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private void DrawValidation()
        {
            var asset = (ConversationAsset)target;

            if (string.IsNullOrWhiteSpace(asset.Id))
            {
                EditorGUILayout.HelpBox("Conversation ID is empty.", MessageType.Error);
            }

            if (asset.Triggers == null || asset.Triggers.Length == 0)
            {
                EditorGUILayout.HelpBox("No triggers assigned. This conversation will never activate.", MessageType.Warning);
            }

            if (asset.Nodes == null || asset.Nodes.Length == 0)
            {
                EditorGUILayout.HelpBox("No nodes authored. Add at least one node.", MessageType.Error);
            }
            else
            {
                // Check for orphaned nodes
                var reachable = new System.Collections.Generic.HashSet<int>();
                CollectReachable(asset, asset.RootNodeId, reachable);

                int orphanCount = asset.Nodes.Length - reachable.Count;
                if (orphanCount > 0)
                {
                    EditorGUILayout.HelpBox($"{orphanCount} node(s) are unreachable from the root node.", MessageType.Warning);
                }

                // Check for missing speakers
                if (asset.RequiredSpirits != null)
                {
                    var requiredSet = new System.Collections.Generic.HashSet<string>();
                    foreach (var s in asset.RequiredSpirits) requiredSet.Add(s.Value);

                    foreach (var node in asset.Nodes)
                    {
                        if (node != null && !string.IsNullOrEmpty(node.Speaker.Value) &&
                            !requiredSet.Contains(node.Speaker.Value))
                        {
                            bool isOptional = false;
                            if (asset.OptionalSpirits != null)
                            {
                                foreach (var opt in asset.OptionalSpirits)
                                {
                                    if (opt.Value == node.Speaker.Value) { isOptional = true; break; }
                                }
                            }

                            if (!isOptional)
                            {
                                EditorGUILayout.HelpBox(
                                    $"Node {node.NodeId} references speaker '{node.Speaker.Value}' who is not in Required or Optional Spirits.",
                                    MessageType.Warning);
                            }
                        }
                    }
                }
            }
        }

        private static void CollectReachable(ConversationAsset asset, int nodeId, System.Collections.Generic.HashSet<int> visited)
        {
            if (nodeId < 0 || nodeId >= asset.Nodes.Length || visited.Contains(nodeId))
                return;

            visited.Add(nodeId);
            var node = asset.Nodes[nodeId];
            if (node?.NextNodeIds == null) return;

            foreach (int next in node.NextNodeIds)
                CollectReachable(asset, next, visited);
        }

        private static bool DrawSection(string title, bool isExpanded, System.Action drawContent)
        {
            EditorGUILayout.Space(4);
            isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, EditorStyles.foldoutHeader);
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                drawContent();
                EditorGUI.indentLevel--;
            }
            return isExpanded;
        }

        private static Color GetSpeakerColor(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return Color.gray;
            return speakerId.ToLowerInvariant() switch
            {
                "zenka" => new Color(0.3f, 0.6f, 1.0f),
                "ignis" => new Color(1.0f, 0.4f, 0.2f),
                "gaia" => new Color(0.3f, 0.85f, 0.4f),
                "spark" => new Color(1.0f, 0.85f, 0.2f),
                _ => new Color(0.6f, 0.6f, 0.6f)
            };
        }
    }
}
