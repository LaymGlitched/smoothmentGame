using System.Collections.Generic;
using GameCode.Spirits.Conversation.Data;
using GameCode.Spirits.Core;
using UnityEngine;

namespace GameCode.Spirits.Conversation.Runtime
{
    /// <summary>
    /// Scores candidate conversations to determine which one should play
    /// when multiple conversations match the same trigger.
    /// 
    /// The scoring formula balances authored importance, contextual relevance,
    /// recency penalties, variety bonuses, and repetition decay to produce
    /// natural-feeling conversation selection without manual priority tuning.
    /// </summary>
    public class ConversationScorer
    {
        private readonly ConversationHistory history;

        // ──────────────────────────────────────────────────────────────
        // Scoring Weights (Tunable)
        // ──────────────────────────────────────────────────────────────

        /// <summary>Bonus score per priority tier level.</summary>
        public float PriorityBonusPerTier { get; set; } = 15f;

        /// <summary>Bonus score per matching context tag.</summary>
        public float ContextTagBonus { get; set; } = 5f;

        /// <summary>Maximum recency penalty applied to recently played conversations.</summary>
        public float MaxRecencyPenalty { get; set; } = 30f;

        /// <summary>Time window in seconds within which recency penalty applies.</summary>
        public float RecencyWindow { get; set; } = 600f;

        /// <summary>Maximum variety bonus for categories that haven't been played recently.</summary>
        public float MaxVarietyBonus { get; set; } = 15f;

        /// <summary>Penalty per previous play of the same conversation.</summary>
        public float RepetitionPenaltyPerPlay { get; set; } = 8f;

        /// <summary>Bonus for never-played one-shot conversations.</summary>
        public float OneShotNoveltyBonus { get; set; } = 50f;

        /// <summary>Penalty for conversations that appear in the recent buffer.</summary>
        public float RecentBufferPenalty { get; set; } = 20f;

        public ConversationScorer(ConversationHistory history)
        {
            this.history = history;
        }

        // ──────────────────────────────────────────────────────────────
        // Scoring
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Scores a single conversation candidate. Higher score = more likely to be selected.
        /// </summary>
        /// <param name="asset">The conversation to score.</param>
        /// <param name="activeContextTags">Tags describing the current gameplay context.</param>
        /// <returns>A floating-point score. Can be negative for heavily penalized conversations.</returns>
        public float Score(ConversationAsset asset, HashSet<string> activeContextTags)
        {
            if (asset == null) return float.MinValue;

            float score = asset.BaseScore;

            // ── Priority Bonus ──
            score += (int)asset.BasePriority * PriorityBonusPerTier;

            // ── Context Tag Bonus ──
            if (asset.ContextTags != null && activeContextTags != null)
            {
                for (int i = 0; i < asset.ContextTags.Length; i++)
                {
                    if (activeContextTags.Contains(asset.ContextTags[i]))
                    {
                        score += ContextTagBonus;
                    }
                }
            }

            string id = asset.Id;

            // ── Recency Penalty ──
            float timeSincePlayed = history.TimeSinceLastPlayed(id);
            if (timeSincePlayed < RecencyWindow)
            {
                // Linearly interpolate: just played → full penalty, RecencyWindow ago → no penalty
                score -= Mathf.Lerp(MaxRecencyPenalty, 0f, timeSincePlayed / RecencyWindow);
            }

            // ── Variety Bonus ──
            float timeSinceCategory = history.TimeSinceCategoryPlayed(asset.Category);
            score += Mathf.Min(timeSinceCategory * 0.1f, MaxVarietyBonus);

            // ── Repetition Penalty ──
            int plays = history.GetPlayCount(id);
            score -= plays * RepetitionPenaltyPerPlay;

            // ── One-Shot Novelty Bonus ──
            if (asset.IsOneShot && plays == 0)
            {
                score += OneShotNoveltyBonus;
            }

            // ── Recent Buffer Penalty ──
            if (history.IsInRecentBuffer(id))
            {
                score -= RecentBufferPenalty;
            }

            return score;
        }

        /// <summary>
        /// Scores and ranks all candidates, returning them sorted highest-score-first.
        /// Allocates a new list. For hot paths, consider scoring in-place.
        /// </summary>
        public List<ScoredConversation> ScoreAndRank(
            IReadOnlyList<ConversationAsset> candidates,
            HashSet<string> activeContextTags)
        {
            var scored = new List<ScoredConversation>(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                float s = Score(candidates[i], activeContextTags);
                scored.Add(new ScoredConversation(candidates[i], s));
            }

            // Sort descending by score
            scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            return scored;
        }
    }

    /// <summary>
    /// Pairs a conversation asset with its computed score for ranked selection.
    /// </summary>
    public readonly struct ScoredConversation
    {
        public readonly ConversationAsset Asset;
        public readonly float Score;

        public ScoredConversation(ConversationAsset asset, float score)
        {
            Asset = asset;
            Score = score;
        }
    }
}
