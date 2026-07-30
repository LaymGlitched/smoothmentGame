using GameCode.Spirits.Communication;
using GameCode.Spirits.Conversation.Data;
using GameCode.Spirits.Core;
using GameCode.Spirits.Runtime;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Runtime
{
    /// <summary>
    /// Resolves a ConversationNode into a concrete, localized DialogueRequest
    /// ready for submission to the SpiritDialogueCoordinator.
    /// 
    /// Handles localization lookup, rich text tag conversion, variant selection,
    /// duration calculation, and priority resolution.
    /// </summary>
    public class DialogueLineResolver
    {
        private readonly Reiteki.Localization.Core.LocalizationManager localizationManager;

        // Track last selected variant per node to avoid immediate repetition
        private readonly System.Collections.Generic.Dictionary<string, int> lastVariantIndex =
            new System.Collections.Generic.Dictionary<string, int>();

        public DialogueLineResolver(Reiteki.Localization.Core.LocalizationManager localizationManager)
        {
            this.localizationManager = localizationManager;
        }

        /// <summary>
        /// Resolves a conversation node into a fully localized DialogueRequest.
        /// Returns null if the speaker is not available or the line cannot be resolved.
        /// </summary>
        /// <param name="node">The conversation node to resolve.</param>
        /// <param name="conversationPriority">The conversation-level priority (used if node doesn't override).</param>
        /// <param name="speaker">The resolved Spirit runtime instance for this node's speaker.</param>
        public DialogueRequest? Resolve(ConversationNode node, PriorityTier conversationPriority, Spirit speaker)
        {
            if (node == null || speaker == null)
                return null;

            // Determine speaker ID string (prefer node.Speaker if set, else speaker.Id)
            string speakerId = !string.IsNullOrEmpty(node.Speaker.Value) ? node.Speaker.Value : speaker.Id;

            // ── Select Dialogue Key ──
            string rawKey = SelectLineKey(node);
            if (string.IsNullOrEmpty(rawKey))
                return null;

            // ── Build Full spirits.json Key Path (e.g., "spark.death.vessel" or "zenka.conversation.hp_critical.intro") ──
            string fullLocKey = BuildDialogueKey(speakerId, rawKey);
            string convLocKey = BuildConversationKey(speakerId, rawKey);

            // ── Resolve Localized Text from spirits.json via LocalizationManager ──
            string localizedText = null;
            if (localizationManager != null)
            {
                if (localizationManager.Has(fullLocKey))
                {
                    localizedText = localizationManager.Get(fullLocKey);
                }
                else if (!string.IsNullOrEmpty(convLocKey) && localizationManager.Has(convLocKey))
                {
                    localizedText = localizationManager.Get(convLocKey);
                }
                else if (localizationManager.Has(rawKey))
                {
                    localizedText = localizationManager.Get(rawKey);
                }
                else
                {
                    localizedText = localizationManager.Get(fullLocKey);
                }
            }
            else
            {
                localizedText = fullLocKey;
            }

            if (string.IsNullOrEmpty(localizedText))
            {
                Debug.LogWarning($"[ConversationSystem] Empty localized text for key '{fullLocKey}' in node {node.NodeId}");
                return null;
            }

            // ── Convert Rich Text Tags ──
            localizedText = ConvertRichTextTags(localizedText);

            // ── Calculate Duration ──
            float duration = node.Duration;
            if (duration <= 0f)
            {
                duration = CalculateDuration(localizedText);
            }

            // ── Resolve Priority ──
            PriorityTier priority = node.OverridePriority ? node.LinePriority : conversationPriority;

            return new DialogueRequest(speaker, localizedText, priority, duration);
        }

        /// <summary>
        /// Combines speaker ID and dialogue key into a single dot-delimited localization path (e.g. "spark" + "death.vessel" -> "spark.death.vessel").
        /// Avoids duplicating the prefix if lineKey already starts with speakerId.
        /// </summary>
        private static string BuildDialogueKey(string speakerId, string lineKey)
        {
            if (string.IsNullOrEmpty(lineKey))
                return string.Empty;

            if (string.IsNullOrEmpty(speakerId))
                return lineKey;

            string prefix = speakerId + ".";
            if (lineKey.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return lineKey;
            }

            return $"{speakerId}.{lineKey}";
        }

        /// <summary>
        /// Attempts to construct a key under the "conversation" namespace if lineKey doesn't already contain it.
        /// E.g. ("zenka", "hp_critical.intro") -> "zenka.conversation.hp_critical.intro"
        /// </summary>
        private static string BuildConversationKey(string speakerId, string lineKey)
        {
            if (string.IsNullOrEmpty(lineKey))
                return string.Empty;

            // If lineKey already contains the conversation namespace, return standard BuildDialogueKey result
            if (lineKey.IndexOf(".conversation.", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                lineKey.StartsWith("conversation.", System.StringComparison.OrdinalIgnoreCase))
            {
                return BuildDialogueKey(speakerId, lineKey);
            }

            // Insert conversation namespace between speakerId and lineKey
            if (!string.IsNullOrEmpty(speakerId))
            {
                string speakerPrefix = speakerId + ".";
                if (lineKey.StartsWith(speakerPrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    string keyWithoutSpeaker = lineKey.Substring(speakerPrefix.Length);
                    return $"{speakerId}.conversation.{keyWithoutSpeaker}";
                }

                return $"{speakerId}.conversation.{lineKey}";
            }

            return $"conversation.{lineKey}";
        }

        /// <summary>
        /// Selects the localization key for a node, using variant selection if available.
        /// </summary>
        private string SelectLineKey(ConversationNode node)
        {
            // If no variants, use the primary key
            if (node.Variants == null || node.Variants.Length == 0)
                return node.LineKey.Value;

            // Build a combined pool: primary + variants
            int totalOptions = 1 + node.Variants.Length;
            string nodeKey = $"{node.NodeId}";

            // Weighted random selection
            if (node.VariantWeights != null && node.VariantWeights.Length == node.Variants.Length)
            {
                // Calculate total weight (including primary key with implicit weight of 1.0)
                float totalWeight = 1.0f;
                for (int i = 0; i < node.VariantWeights.Length; i++)
                    totalWeight += node.VariantWeights[i];

                float roll = Random.Range(0f, totalWeight);
                float accumulated = 1.0f; // Primary key weight

                if (roll < accumulated)
                    return node.LineKey.Value;

                for (int i = 0; i < node.Variants.Length; i++)
                {
                    accumulated += node.VariantWeights[i];
                    if (roll < accumulated)
                    {
                        // Avoid repeating the same variant back-to-back
                        if (lastVariantIndex.TryGetValue(nodeKey, out int lastIdx) && lastIdx == i && node.Variants.Length > 1)
                        {
                            int next = (i + 1) % node.Variants.Length;
                            lastVariantIndex[nodeKey] = next;
                            return node.Variants[next].Value;
                        }

                        lastVariantIndex[nodeKey] = i;
                        return node.Variants[i].Value;
                    }
                }

                // Fallback (shouldn't happen)
                return node.LineKey.Value;
            }

            // Uniform random if no weights specified
            int selected = Random.Range(0, totalOptions);
            if (selected == 0)
                return node.LineKey.Value;

            return node.Variants[selected - 1].Value;
        }

        /// <summary>
        /// Converts custom rich text markup tags to Unity's TextMeshPro-compatible tags.
        /// Matches the existing convention from DialogueResolverService.
        /// </summary>
        private static string ConvertRichTextTags(string text)
        {
            text = text.Replace("[b]", "<b>").Replace("[/b]", "</b>");
            text = text.Replace("[i]", "<i>").Replace("[/i]", "</i>");
            return text;
        }

        /// <summary>
        /// Calculates display duration from word count using the same heuristic
        /// as the existing DialogueResolverService.
        /// </summary>
        private static float CalculateDuration(string text)
        {
            int wordCount = text.Split(
                new char[] { ' ', '\n', '\r' },
                System.StringSplitOptions.RemoveEmptyEntries
            ).Length;

            float calculatedDuration = 1.0f + (wordCount * 0.4f);
            return Mathf.Clamp(calculatedDuration, 2.0f, 8.0f);
        }
    }
}
