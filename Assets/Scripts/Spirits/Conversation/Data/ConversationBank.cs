using System.Collections.Generic;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Data
{
    /// <summary>
    /// Registry of all ConversationAssets, indexed by trigger for O(1) lookup.
    /// The ConversationDirector queries this bank when a trigger fires to get
    /// all candidate conversations that could respond to that event.
    /// 
    /// The index is built at runtime from the flat asset list to avoid
    /// requiring manual trigger-to-asset mapping in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Spirits/Conversation/Conversation Bank", fileName = "ConversationBank")]
    public class ConversationBank : ScriptableObject
    {
        [Tooltip("All authored conversation assets. The bank auto-indexes these by trigger at runtime.")]
        [SerializeField] private ConversationAsset[] conversations;

        // Runtime index: trigger → list of conversations that respond to it
        private Dictionary<string, List<ConversationAsset>> triggerIndex;
        private bool isIndexBuilt;

        /// <summary>
        /// Read-only access to the full conversation list for iteration/debugging.
        /// </summary>
        public IReadOnlyList<ConversationAsset> AllConversations => conversations;

        /// <summary>
        /// Returns all conversations that list the given trigger. 
        /// Returns an empty list if no conversations match.
        /// Builds the index lazily on first access.
        /// </summary>
        public IReadOnlyList<ConversationAsset> GetByTrigger(ConversationTrigger trigger)
        {
            EnsureIndexBuilt();

            if (string.IsNullOrEmpty(trigger.Value))
                return System.Array.Empty<ConversationAsset>();

            if (triggerIndex.TryGetValue(trigger.Value, out var results))
                return results;

            return System.Array.Empty<ConversationAsset>();
        }

        /// <summary>
        /// Returns all conversations that list the given trigger string.
        /// Convenience overload.
        /// </summary>
        public IReadOnlyList<ConversationAsset> GetByTrigger(string triggerValue)
        {
            return GetByTrigger(new ConversationTrigger(triggerValue));
        }

        /// <summary>
        /// Forces the index to rebuild. Call after modifying the conversations array
        /// at runtime (which should be rare).
        /// </summary>
        public void RebuildIndex()
        {
            isIndexBuilt = false;
            EnsureIndexBuilt();
        }

        /// <summary>
        /// Returns the total number of registered conversations.
        /// </summary>
        public int Count => conversations != null ? conversations.Length : 0;

        private void EnsureIndexBuilt()
        {
            if (isIndexBuilt)
                return;

            triggerIndex = new Dictionary<string, List<ConversationAsset>>();

            if (conversations == null)
            {
                isIndexBuilt = true;
                return;
            }

            for (int i = 0; i < conversations.Length; i++)
            {
                var asset = conversations[i];
                if (asset == null || asset.Triggers == null)
                    continue;

                for (int t = 0; t < asset.Triggers.Length; t++)
                {
                    string key = asset.Triggers[t].Value;
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!triggerIndex.TryGetValue(key, out var list))
                    {
                        list = new List<ConversationAsset>(4);
                        triggerIndex.Add(key, list);
                    }

                    list.Add(asset);
                }
            }

            isIndexBuilt = true;
        }

        private void OnEnable()
        {
            // Force re-index when the asset is loaded or re-imported
            isIndexBuilt = false;
        }

        private void OnValidate()
        {
            // Force re-index when edited in the Inspector
            isIndexBuilt = false;
        }
    }
}
