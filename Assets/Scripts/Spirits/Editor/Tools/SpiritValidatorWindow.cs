using System.Collections.Generic;
using System.Linq;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Tools
{
    public class SpiritValidatorWindow : EditorWindow
    {
        private List<string> errors = new List<string>();
        private List<string> warnings = new List<string>();
        private List<string> infos = new List<string>();

        private Vector2 scrollPos;

        [MenuItem("Spirits/Analyze Spirit System", priority = 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<SpiritValidatorWindow>("Spirit Analyzer");
            window.RunValidation();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Run Analysis", GUILayout.Height(30)))
            {
                RunValidation();
            }

            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (errors.Count > 0)
            {
                EditorGUILayout.LabelField($"Errors ({errors.Count})", EditorStyles.boldLabel);
                foreach (var err in errors)
                {
                    EditorGUILayout.HelpBox(err, MessageType.Error);
                }
                EditorGUILayout.Space();
            }

            if (warnings.Count > 0)
            {
                EditorGUILayout.LabelField($"Warnings ({warnings.Count})", EditorStyles.boldLabel);
                foreach (var warn in warnings)
                {
                    EditorGUILayout.HelpBox(warn, MessageType.Warning);
                }
                EditorGUILayout.Space();
            }

            if (infos.Count > 0)
            {
                EditorGUILayout.LabelField($"Info ({infos.Count})", EditorStyles.boldLabel);
                foreach (var info in infos)
                {
                    EditorGUILayout.HelpBox(info, MessageType.Info);
                }
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("All clear! No issues found.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunValidation()
        {
            errors.Clear();
            warnings.Clear();
            infos.Clear();

            // 1. Check Topic Definitions
            var topicGuids = AssetDatabase.FindAssets("t:TopicDefinition");
            var topics = topicGuids.Select(g => AssetDatabase.LoadAssetAtPath<TopicDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var topicIds = new HashSet<string>();

            foreach (var topic in topics)
            {
                if (string.IsNullOrWhiteSpace(topic.Id.Value))
                    errors.Add($"Topic '{topic.name}' has an empty ID.");
                else if (!topicIds.Add(topic.Id.Value))
                    errors.Add($"Duplicate Topic ID found: {topic.Id.Value} in '{topic.name}'");
            }
            infos.Add($"Found {topics.Count} Topic Definitions.");

            // 2. Check Spirit Definitions
            var spiritGuids = AssetDatabase.FindAssets("t:SpiritDefinition");
            var spirits = spiritGuids.Select(g => AssetDatabase.LoadAssetAtPath<SpiritDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var spiritIds = new HashSet<string>();

            foreach (var spirit in spirits)
            {
                if (string.IsNullOrWhiteSpace(spirit.Id.Value))
                    errors.Add($"Spirit '{spirit.name}' has an empty ID.");
                else if (!spiritIds.Add(spirit.Id.Value))
                    errors.Add($"Duplicate Spirit ID found: {spirit.Id.Value} in '{spirit.name}'");

                if (spirit.CommunicationProfile == null)
                    warnings.Add($"Spirit '{spirit.name}' is missing a Communication Profile.");
                if (spirit.IdentityProfile == null)
                    warnings.Add($"Spirit '{spirit.name}' is missing an Identity Profile.");
            }
            infos.Add($"Found {spirits.Count} Spirit Definitions.");

            // 3. Check Communication Profiles
            var commGuids = AssetDatabase.FindAssets("t:SpiritCommunicationProfile");
            var comms = commGuids.Select(g => AssetDatabase.LoadAssetAtPath<SpiritCommunicationProfile>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            
            foreach (var comm in comms)
            {
                if (comm.Entries.Count == 0)
                {
                    warnings.Add($"Communication Profile '{comm.name}' has no dialogue entries.");
                }
                foreach (var entry in comm.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Topic.Value))
                        errors.Add($"Profile '{comm.name}' has an entry with an empty Topic ID.");
                    else if (!topicIds.Contains(entry.Topic.Value))
                        errors.Add($"Profile '{comm.name}' uses undefined Topic ID: '{entry.Topic.Value}'. Please create a TopicDefinition for it.");

                    if (string.IsNullOrWhiteSpace(entry.LocalizationKey.Value))
                        errors.Add($"Profile '{comm.name}' has an entry with an empty Localization Key.");
                }
            }

            // (Further checks for Events, Scenarios, missing audio, etc. would go here)
        }
    }
}
