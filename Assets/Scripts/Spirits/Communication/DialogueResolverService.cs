using System.Collections.Generic;
using GameCode.Spirits.Data;
using UnityEngine;

namespace GameCode.Spirits.Communication
{
    /// <summary>
    /// Pure C# Service responsible for translating abstract CommunicationIntents 
    /// into concrete DialogueRequests using data-driven SpiritCommunicationProfiles.
    /// </summary>
    public class DialogueResolverService : IDialogueResolver
    {
        private readonly Reiteki.Localization.Core.LocalizationManager _localizationManager;
        private readonly Dictionary<string, string> _lastPlayedKeys = new Dictionary<string, string>();

        public DialogueResolverService(Reiteki.Localization.Core.LocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
        }

        public DialogueRequest? ResolveIntent(CommunicationIntent intent)
        {
            var profile = intent.SourceSpirit.Definition.CommunicationProfile;

            if (profile == null || profile.Entries.Count == 0)
            {
                return null; // Spirit has no authored dialogue
            }

            // Filter for entries that match the Topic and Priority
            var validEntries = new List<DialogueEntry>();
            foreach (var entry in profile.Entries)
            {
                if (entry.Topic == intent.Topic && entry.Priority == intent.Priority)
                {
                    validEntries.Add(entry);
                }
            }

            if (validEntries.Count == 0)
            {
                return null; // No matching line authored for this scenario
            }

            // Filter out the last played entry to prevent repetition if possible
            string historyKey = $"{intent.SourceSpirit.Id}_{intent.Topic}";
            if (validEntries.Count > 1 && _lastPlayedKeys.TryGetValue(historyKey, out string lastPlayedKey))
            {
                var filteredEntries = validEntries.FindAll(e => e.LocalizationKey.Value != lastPlayedKey);
                if (filteredEntries.Count > 0)
                {
                    validEntries = filteredEntries;
                }
            }

            // Pick a random valid entry (Future: Use Weights and CooldownGroups)
            var selectedEntry = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
            
            // Record this entry as the last played for this spirit and topic
            _lastPlayedKeys[historyKey] = selectedEntry.LocalizationKey.Value;

            // Resolve localization here
            string localizedText = _localizationManager != null 
                ? _localizationManager.Get(selectedEntry.LocalizationKey.Value) 
                : selectedEntry.LocalizationKey.Value;

            // Calculate duration
            float duration = selectedEntry.Duration;
            if (duration <= 0f)
            {
                // Word count heuristic
                int wordCount = localizedText.Split(new char[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
                
                // Base time + time per word
                float calculatedDuration = 1.0f + (wordCount * 0.4f);
                
                // Clamp to sensible minimum and maximum values
                duration = Mathf.Clamp(calculatedDuration, 2.0f, 8.0f);
            }

            return new DialogueRequest(
                intent.SourceSpirit, 
                localizedText, 
                selectedEntry.Priority, 
                duration
            );
        }
    }
}
