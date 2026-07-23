using System.Collections.Generic;
using GameCode.Spirits.Conversation.Data;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Runtime
{
    /// <summary>
    /// Manages cooldown timers at multiple granularities to prevent dialogue spam.
    /// All checks use Time.time comparisons — no Update loops required.
    /// 
    /// Cooldown levels:
    /// 1. Per-Conversation — prevents the same conversation from replaying too soon
    /// 2. Per-Group — shared cooldown across multiple conversations (e.g., "HPWarnings")
    /// 3. Per-Category — prevents too many of the same category in succession
    /// 4. Global — minimum gap between ANY two conversations
    /// </summary>
    public class ConversationCooldownManager
    {
        // ConversationId → expiry time
        private readonly Dictionary<string, float> conversationCooldowns = new Dictionary<string, float>();

        // CooldownGroup → expiry time
        private readonly Dictionary<string, float> groupCooldowns = new Dictionary<string, float>();

        // Category → expiry time
        private readonly Dictionary<ConversationCategory, float> categoryCooldowns = new Dictionary<ConversationCategory, float>();

        // Global cooldown expiry
        private float globalCooldownExpiry;

        // ──────────────────────────────────────────────────────────────
        // Configuration
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Minimum gap in seconds between any two conversations.
        /// Prevents the system from feeling like it's spamming dialogue.
        /// </summary>
        public float GlobalCooldownDuration { get; set; } = 3.0f;

        /// <summary>
        /// Default cooldown per category. Writers can override per-conversation.
        /// </summary>
        public float DefaultCategoryCooldown { get; set; } = 15.0f;

        // ──────────────────────────────────────────────────────────────
        // Starting Cooldowns
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts all relevant cooldowns after a conversation begins or completes.
        /// </summary>
        public void StartCooldowns(ConversationAsset asset)
        {
            if (asset == null) return;

            float now = Time.time;

            // Per-conversation cooldown
            if (asset.CooldownDuration > 0f)
            {
                conversationCooldowns[asset.Id] = now + asset.CooldownDuration;
            }

            // Per-group cooldown (shares the conversation's cooldown duration)
            if (!string.IsNullOrEmpty(asset.CooldownGroup) && asset.CooldownDuration > 0f)
            {
                groupCooldowns[asset.CooldownGroup] = now + asset.CooldownDuration;
            }

            // Per-category cooldown
            categoryCooldowns[asset.Category] = now + DefaultCategoryCooldown;

            // Global cooldown
            globalCooldownExpiry = now + GlobalCooldownDuration;
        }

        // ──────────────────────────────────────────────────────────────
        // Checking Cooldowns
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the global cooldown is active (any conversation is on cooldown).
        /// </summary>
        public bool IsGlobalCooldownActive()
        {
            return Time.time < globalCooldownExpiry;
        }

        /// <summary>
        /// Returns true if the specific conversation is on cooldown.
        /// </summary>
        public bool IsOnCooldown(string conversationId)
        {
            if (conversationCooldowns.TryGetValue(conversationId, out float expiry))
                return Time.time < expiry;
            return false;
        }

        /// <summary>
        /// Returns true if the cooldown group is active.
        /// </summary>
        public bool IsGroupOnCooldown(string cooldownGroup)
        {
            if (string.IsNullOrEmpty(cooldownGroup)) return false;

            if (groupCooldowns.TryGetValue(cooldownGroup, out float expiry))
                return Time.time < expiry;
            return false;
        }

        /// <summary>
        /// Returns true if the category cooldown is active.
        /// </summary>
        public bool IsCategoryOnCooldown(ConversationCategory category)
        {
            if (categoryCooldowns.TryGetValue(category, out float expiry))
                return Time.time < expiry;
            return false;
        }

        /// <summary>
        /// Checks ALL cooldown levels for a specific conversation.
        /// Returns true if ANY cooldown blocks this conversation from playing.
        /// </summary>
        public bool IsBlocked(ConversationAsset asset)
        {
            if (asset == null) return true;

            if (IsGlobalCooldownActive()) return true;
            if (IsOnCooldown(asset.Id)) return true;
            if (IsGroupOnCooldown(asset.CooldownGroup)) return true;
            // Category cooldown is a soft check — only blocks Ambient/Reaction
            if (asset.BasePriority <= Core.PriorityTier.Standard && IsCategoryOnCooldown(asset.Category))
                return true;

            return false;
        }

        // ──────────────────────────────────────────────────────────────
        // Maintenance
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears all active cooldowns. Use when loading a save or starting a new session.
        /// </summary>
        public void ClearAll()
        {
            conversationCooldowns.Clear();
            groupCooldowns.Clear();
            categoryCooldowns.Clear();
            globalCooldownExpiry = 0f;
        }

        /// <summary>
        /// Removes expired cooldown entries to prevent dictionary bloat over long play sessions.
        /// Call opportunistically (not every frame).
        /// </summary>
        public void PurgeExpired()
        {
            float now = Time.time;
            List<string> keysToRemove = null;

            foreach (var kvp in conversationCooldowns)
            {
                if (kvp.Value <= now)
                {
                    keysToRemove ??= new List<string>();
                    keysToRemove.Add(kvp.Key);
                }
            }

            if (keysToRemove != null)
            {
                foreach (var key in keysToRemove)
                    conversationCooldowns.Remove(key);
            }

            keysToRemove?.Clear();

            foreach (var kvp in groupCooldowns)
            {
                if (kvp.Value <= now)
                {
                    keysToRemove ??= new List<string>();
                    keysToRemove.Add(kvp.Key);
                }
            }

            if (keysToRemove != null)
            {
                foreach (var key in keysToRemove)
                    groupCooldowns.Remove(key);
            }
        }
    }
}
