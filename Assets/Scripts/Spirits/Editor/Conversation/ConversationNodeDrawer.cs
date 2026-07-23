using GameCode.Spirits.Conversation.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Conversation
{
    /// <summary>
    /// Custom property drawer for ConversationNode. Displays each node as a clean,
    /// color-coded card in the ConversationAsset Inspector with speaker name, line preview,
    /// timing info, and flow indicators.
    /// </summary>
    [CustomPropertyDrawer(typeof(ConversationNode))]
    public class ConversationNodeDrawer : PropertyDrawer
    {
        private const float HeaderHeight = 24f;
        private const float Padding = 6f;
        private const float Spacing = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return HeaderHeight + 2f;

            float height = HeaderHeight + Padding * 2f;

            // Calculate height of all expanded fields dynamically
            var speaker = property.FindPropertyRelative("Speaker");
            var lineKey = property.FindPropertyRelative("LineKey");
            var variants = property.FindPropertyRelative("Variants");
            var variantWeights = property.FindPropertyRelative("VariantWeights");
            var duration = property.FindPropertyRelative("Duration");
            var replyDelay = property.FindPropertyRelative("ReplyDelay");
            var overridePriority = property.FindPropertyRelative("OverridePriority");
            var linePriority = property.FindPropertyRelative("LinePriority");
            var nextNodeIds = property.FindPropertyRelative("NextNodeIds");
            var nextNodeWeights = property.FindPropertyRelative("NextNodeWeights");
            var nextNodeConditions = property.FindPropertyRelative("NextNodeConditions");

            if (speaker != null) height += EditorGUI.GetPropertyHeight(speaker, true) + Spacing;
            if (lineKey != null) height += EditorGUI.GetPropertyHeight(lineKey, true) + Spacing;

            if (variants != null)
            {
                height += EditorGUI.GetPropertyHeight(variants, true) + Spacing;
                if (variants.isArray && variants.arraySize > 0 && variantWeights != null)
                {
                    height += EditorGUI.GetPropertyHeight(variantWeights, true) + Spacing;
                }
            }

            if (duration != null) height += EditorGUI.GetPropertyHeight(duration, true) + Spacing;
            if (replyDelay != null) height += EditorGUI.GetPropertyHeight(replyDelay, true) + Spacing;

            if (overridePriority != null)
            {
                height += EditorGUI.GetPropertyHeight(overridePriority, true) + Spacing;
                if (overridePriority.boolValue && linePriority != null)
                {
                    height += EditorGUI.GetPropertyHeight(linePriority, true) + Spacing;
                }
            }

            if (nextNodeIds != null)
            {
                height += EditorGUI.GetPropertyHeight(nextNodeIds, true) + Spacing;

                if (nextNodeIds.isArray && nextNodeIds.arraySize > 1 && nextNodeWeights != null)
                {
                    height += EditorGUI.GetPropertyHeight(nextNodeWeights, true) + Spacing;
                }

                if (nextNodeIds.isArray && nextNodeIds.arraySize > 0 && nextNodeConditions != null)
                {
                    height += EditorGUI.GetPropertyHeight(nextNodeConditions, true) + Spacing;
                }
            }

            return height + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var nodeId = property.FindPropertyRelative("NodeId");
            var speaker = property.FindPropertyRelative("Speaker");
            var lineKey = property.FindPropertyRelative("LineKey");
            var variants = property.FindPropertyRelative("Variants");
            var variantWeights = property.FindPropertyRelative("VariantWeights");
            var duration = property.FindPropertyRelative("Duration");
            var replyDelay = property.FindPropertyRelative("ReplyDelay");
            var overridePriority = property.FindPropertyRelative("OverridePriority");
            var linePriority = property.FindPropertyRelative("LinePriority");
            var nextNodeIds = property.FindPropertyRelative("NextNodeIds");
            var nextNodeWeights = property.FindPropertyRelative("NextNodeWeights");
            var nextNodeConditions = property.FindPropertyRelative("NextNodeConditions");

            var isTerminal = nextNodeIds == null || !nextNodeIds.isArray || nextNodeIds.arraySize == 0;

            // ── Speaker Color Header ──
            string speakerValue = speaker?.FindPropertyRelative("Value")?.stringValue ?? "???";
            Color speakerColor = GetSpeakerColor(speakerValue);

            // Card container rect
            float totalHeight = GetPropertyHeight(property, label);
            Rect cardRect = new Rect(position.x, position.y, position.width, totalHeight - 2f);

            // Outer card background
            Color cardBg = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.9f) : new Color(0.88f, 0.88f, 0.88f, 0.9f);
            EditorGUI.DrawRect(cardRect, cardBg);

            // Header rect
            Rect headerRect = new Rect(position.x, position.y, position.width, HeaderHeight);
            Color headerBg = new Color(speakerColor.r, speakerColor.g, speakerColor.b, 0.25f);
            EditorGUI.DrawRect(headerRect, headerBg);

            // Node ID Badge
            Rect badgeRect = new Rect(headerRect.x + 4f, headerRect.y + 3f, 26f, HeaderHeight - 6f);
            EditorGUI.DrawRect(badgeRect, speakerColor);
            GUI.Label(badgeRect, $"{nodeId?.intValue ?? 0}", new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            });

            // Speaker Name Label
            Rect speakerLabelRect = new Rect(badgeRect.xMax + 8f, headerRect.y + 3f, 90f, HeaderHeight - 6f);
            GUI.Label(speakerLabelRect, string.IsNullOrEmpty(speakerValue) ? "UNASSIGNED" : speakerValue.ToUpperInvariant(), new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = speakerColor },
                alignment = TextAnchor.MiddleLeft
            });

            // Flow Indicator (Right side of header)
            string flowLabel = isTerminal ? "■ END" : $"→ [{FormatNextNodes(nextNodeIds)}]";
            Color flowColor = isTerminal ? new Color(1.0f, 0.4f, 0.4f) : new Color(0.4f, 0.9f, 0.5f);
            Rect flowRect = new Rect(headerRect.xMax - 110f, headerRect.y + 3f, 104f, HeaderHeight - 6f);
            GUI.Label(flowRect, flowLabel, new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = flowColor },
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            });

            // Foldout Click Area across the header
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, headerRect.width - 115f, headerRect.height);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);

            if (!property.isExpanded)
            {
                // Draw collapsed text preview inside header foldout space
                string lineKeyValue = lineKey?.FindPropertyRelative("Value")?.stringValue ?? "";
                if (!string.IsNullOrEmpty(lineKeyValue))
                {
                    Rect previewRect = new Rect(speakerLabelRect.xMax + 4f, headerRect.y + 3f, headerRect.width - speakerLabelRect.xMax - 124f, HeaderHeight - 6f);
                    GUI.Label(previewRect, lineKeyValue, new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.3f, 0.3f, 0.3f) },
                        clipping = TextClipping.Clip
                    });
                }

                EditorGUI.EndProperty();
                return;
            }

            // ── Expanded Body ──
            float currentY = headerRect.yMax + Padding;
            float contentWidth = position.width - (Padding * 2f);
            float contentX = position.x + Padding;

            var indent = EditorGUI.indentLevel;

            void DrawField(SerializedProperty prop)
            {
                if (prop == null) return;
                float h = EditorGUI.GetPropertyHeight(prop, true);
                Rect fieldRect = new Rect(contentX, currentY, contentWidth, h);
                EditorGUI.PropertyField(fieldRect, prop, true);
                currentY += h + Spacing;
            }

            DrawField(speaker);
            DrawField(lineKey);

            if (variants != null)
            {
                DrawField(variants);
                if (variants.isArray && variants.arraySize > 0 && variantWeights != null)
                {
                    DrawField(variantWeights);
                }
            }

            DrawField(duration);
            DrawField(replyDelay);

            if (overridePriority != null)
            {
                DrawField(overridePriority);
                if (overridePriority.boolValue && linePriority != null)
                {
                    DrawField(linePriority);
                }
            }

            if (nextNodeIds != null)
            {
                DrawField(nextNodeIds);

                if (nextNodeIds.isArray && nextNodeIds.arraySize > 1 && nextNodeWeights != null)
                {
                    DrawField(nextNodeWeights);
                }

                if (nextNodeIds.isArray && nextNodeIds.arraySize > 0 && nextNodeConditions != null)
                {
                    DrawField(nextNodeConditions);
                }
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static string FormatNextNodes(SerializedProperty nextNodeIds)
        {
            if (nextNodeIds == null || !nextNodeIds.isArray || nextNodeIds.arraySize == 0) return "";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < nextNodeIds.arraySize; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(nextNodeIds.GetArrayElementAtIndex(i).intValue);
            }
            return sb.ToString();
        }

        private static Color GetSpeakerColor(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return Color.gray;

            return speakerId.ToLowerInvariant() switch
            {
                "zenka" => new Color(0.3f, 0.6f, 1.0f),     // Cool Blue
                "ignis" => new Color(1.0f, 0.4f, 0.2f),     // Fiery Orange
                "gaia" => new Color(0.3f, 0.85f, 0.4f),     // Earthy Green
                "spark" => new Color(1.0f, 0.85f, 0.2f),    // Electric Yellow
                _ => new Color(0.6f, 0.6f, 0.6f)            // Default: Gray
            };
        }
    }
}
