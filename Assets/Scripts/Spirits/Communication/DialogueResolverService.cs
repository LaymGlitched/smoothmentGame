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

            // Pick a random valid entry (Future: Use Weights and CooldownGroups)
            var selectedEntry = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];

            // Calculate duration
            float duration = selectedEntry.Duration;
            if (duration <= 0f)
            {
                // Fallback heuristic: 0.05s per character + 1s base. 
                // Since we only have localization keys right now, we estimate based on the key length 
                // or just use a standard default until localization is fully implemented.
                duration = 3.0f; 
            }

            return new DialogueRequest(
                intent.SourceSpirit, 
                selectedEntry.LocalizationKey, 
                selectedEntry.Priority, 
                duration
            );
        }
    }
}
