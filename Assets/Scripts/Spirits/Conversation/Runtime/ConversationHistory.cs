using System.Collections.Generic;
using GameCode.Spirits.Conversation.Data;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Runtime
{
    /// <summary>
    /// Tracks the play history of all conversations for the current session/playthrough.
    /// Provides fast lookups for recency, play count, and one-shot completion status.
    /// 
    /// Used by the ConversationScorer to penalize recently played conversations,
    /// and by the ConversationDirector to enforce one-shot restrictions.
    /// 
    /// Designed to be serializable for save/load persistence of one-shot conversations.
    /// </summary>
    public class ConversationHistory
    {
        private readonly List<ConversationRecord> records = new List<ConversationRecord>();

        // Fast lookup caches (rebuilt from records if needed)
        private readonly Dictionary<string, float> lastPlayedTime = new Dictionary<string, float>();
        private readonly Dictionary<string, int> playCount = new Dictionary<string, int>();
        private readonly Dictionary<ConversationCategory, float> lastCategoryTime = new Dictionary<ConversationCategory, float>();

        // Circular buffer of recent conversation IDs to detect clustering
        private readonly Queue<string> recentBuffer = new Queue<string>();
        private const int RecentBufferSize = 20;

        // ──────────────────────────────────────────────────────────────
        // Recording
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Records a completed, cancelled, or interrupted conversation.
        /// </summary>
        public void Record(ConversationInstance instance)
        {
            if (instance == null || instance.Asset == null)
                return;

            string id = instance.Asset.Id;
            var category = instance.Asset.Category;

            var record = new ConversationRecord
            {
                ConversationId = id,
                Category = category,
                StartTime = instance.StartTime,
                EndTime = Time.time,
                FinalState = instance.State,
                LastNodeReached = instance.CurrentNodeId,
                TotalNodesPlayed = instance.NodesPlayed
            };

            records.Add(record);

            // Update caches
            lastPlayedTime[id] = Time.time;
            lastCategoryTime[category] = Time.time;

            if (playCount.TryGetValue(id, out int count))
                playCount[id] = count + 1;
            else
                playCount[id] = 1;

            // Update recent buffer
            recentBuffer.Enqueue(id);
            if (recentBuffer.Count > RecentBufferSize)
                recentBuffer.Dequeue();
        }

        // ──────────────────────────────────────────────────────────────
        // Queries
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the time in seconds since the given conversation was last played.
        /// Returns float.MaxValue if never played.
        /// </summary>
        public float TimeSinceLastPlayed(string conversationId)
        {
            if (lastPlayedTime.TryGetValue(conversationId, out float time))
                return Time.time - time;
            return float.MaxValue;
        }

        /// <summary>
        /// Returns the time in seconds since any conversation of the given category was played.
        /// Returns float.MaxValue if no conversation of that category has ever played.
        /// </summary>
        public float TimeSinceCategoryPlayed(ConversationCategory category)
        {
            if (lastCategoryTime.TryGetValue(category, out float time))
                return Time.time - time;
            return float.MaxValue;
        }

        /// <summary>
        /// Returns how many times the given conversation has been played (completed or not).
        /// </summary>
        public int GetPlayCount(string conversationId)
        {
            return playCount.TryGetValue(conversationId, out int count) ? count : 0;
        }

        /// <summary>
        /// Returns true if the conversation has ever been played to completion.
        /// Critical for one-shot enforcement.
        /// </summary>
        public bool HasCompleted(string conversationId)
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (records[i].ConversationId == conversationId &&
                    records[i].FinalState == ConversationState.Completed)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the conversation has been played at all (completed or not).
        /// </summary>
        public bool HasPlayed(string conversationId)
        {
            return playCount.ContainsKey(conversationId);
        }

        /// <summary>
        /// Returns the last record for the given conversation, or null if never played.
        /// </summary>
        public ConversationRecord? GetLastRecord(string conversationId)
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (records[i].ConversationId == conversationId)
                    return records[i];
            }
            return null;
        }

        /// <summary>
        /// Returns true if the conversation appears in the recent buffer.
        /// Used to detect excessive repetition.
        /// </summary>
        public bool IsInRecentBuffer(string conversationId)
        {
            return recentBuffer.Contains(conversationId);
        }

        /// <summary>
        /// Read-only access to all records for debugging and save/load.
        /// </summary>
        public IReadOnlyList<ConversationRecord> AllRecords => records;

        /// <summary>
        /// Total number of conversations recorded.
        /// </summary>
        public int TotalRecords => records.Count;

        // ──────────────────────────────────────────────────────────────
        // Persistence
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears all non-persistent history (ambient, reaction, bond conversations).
        /// Retains one-shot story completion records across sessions.
        /// Call this when starting a new gameplay session but NOT a new playthrough.
        /// </summary>
        public void ClearSessionHistory()
        {
            // Preserve one-shot completions
            var preserved = new List<ConversationRecord>();
            foreach (var record in records)
            {
                if (record.FinalState == ConversationState.Completed &&
                    record.Category == ConversationCategory.Story)
                {
                    preserved.Add(record);
                }
            }

            records.Clear();
            lastPlayedTime.Clear();
            playCount.Clear();
            lastCategoryTime.Clear();
            recentBuffer.Clear();

            // Re-add preserved records and rebuild caches
            foreach (var record in preserved)
            {
                records.Add(record);
                lastPlayedTime[record.ConversationId] = record.EndTime;
                playCount[record.ConversationId] = 1;
            }
        }

        /// <summary>
        /// Clears ALL history. Use when starting a completely new playthrough.
        /// </summary>
        public void ClearAll()
        {
            records.Clear();
            lastPlayedTime.Clear();
            playCount.Clear();
            lastCategoryTime.Clear();
            recentBuffer.Clear();
        }
    }

    /// <summary>
    /// Immutable record of a conversation that has been played.
    /// </summary>
    [System.Serializable]
    public struct ConversationRecord
    {
        public string ConversationId;
        public ConversationCategory Category;
        public float StartTime;
        public float EndTime;
        public ConversationState FinalState;
        public int LastNodeReached;
        public int TotalNodesPlayed;
    }
}
