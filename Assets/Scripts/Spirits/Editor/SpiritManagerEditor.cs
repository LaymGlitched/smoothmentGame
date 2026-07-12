using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GameCode.Spirits.Runtime;
using GameCode.Spirits.Agency;
using GameCode.Spirits.Memory;
using GameCode.Spirits.Core;

namespace GameCode.Spirits.EditorScripts
{
    [CustomEditor(typeof(SpiritManager))]
    public class SpiritManagerEditor : Editor
    {
        public override bool RequiresConstantRepaint()
        {
            // Ensures progress bars animate and timestamps update continuously while playing
            return EditorApplication.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            // Draw the default inspector (Configuration array, etc.)
            base.OnInspectorGUI();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view Spirit cognition states.", MessageType.Info);
                return;
            }

            var manager = (SpiritManager)target;
            var spirits = manager.GetAllSpirits();

            if (spirits == null || spirits.Count == 0)
            {
                EditorGUILayout.HelpBox("No active Spirits found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spirit Cognition State", EditorStyles.boldLabel);

            foreach (var spirit in spirits)
            {
                DrawSpiritFoldout(spirit);
            }
        }

        private void DrawSpiritFoldout(Spirit spirit)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            string title = $"{spirit.Definition.DisplayName} ({spirit.Id})";
            bool isForegrounded = spirit.RuntimeData.CurrentPresenceMode == PresenceMode.Foregrounded;
            if (isForegrounded) title += " [FOREGROUNDED]";

            // Store foldout state in a temporary dictionary or use EditorGUI utility. For simplicity, we can use EditorPrefs or just keep it open.
            // Using a simple foldout based on spirit ID
            string foldoutKey = $"SpiritDebug_{spirit.Id}";
            bool foldout = EditorPrefs.GetBool(foldoutKey, false);
            bool newFoldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            
            if (newFoldout != foldout)
            {
                EditorPrefs.SetBool(foldoutKey, newFoldout);
            }

            if (newFoldout)
            {
                EditorGUI.indentLevel++;
                
                DrawIdentitySection(spirit);
                DrawMemorySection(spirit);
                DrawAgencySection(spirit);
                DrawRelationshipsSection(spirit);
                DrawCommunicationSection(spirit);
                DrawRuntimeStatusSection(spirit);
                DrawDebugButtons(spirit);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawIdentitySection(Spirit spirit)
        {
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Name", spirit.Definition.DisplayName);
            EditorGUILayout.LabelField("ID", spirit.Id);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawMemorySection(Spirit spirit)
        {
            EditorGUILayout.LabelField("Recent Memories", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            var memories = spirit.Memory.RecentMemory;
            if (memories.Count == 0)
            {
                EditorGUILayout.LabelField("No recent memories.");
            }
            else
            {
                foreach (var mem in memories)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    float age = Time.time - mem.Timestamp;
                    EditorGUILayout.LabelField($"Event Type", mem.RawEvent.GetType().Name);
                    EditorGUILayout.LabelField($"Age", $"{age:F1}s ago");
                    EditorGUILayout.LabelField($"Interpretation", mem.Tag.ToString());
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), mem.Significance, $"Significance: {mem.Significance:F2}");
                    EditorGUILayout.EndVertical();
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawAgencySection(Spirit spirit)
        {
            EditorGUILayout.LabelField("Current Concerns", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            var concerns = spirit.Agency.ActiveConcerns;
            if (concerns.Count == 0)
            {
                EditorGUILayout.LabelField("No active concerns.");
            }
            else
            {
                foreach (var concern in concerns)
                {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), concern.Intensity, $"{concern.Subject}: {concern.Intensity:F2}");
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawRelationshipsSection(Spirit spirit)
        {
            EditorGUILayout.LabelField("Relationships", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            var bonds = spirit.Relationships.Bonds;
            if (bonds.Count == 0)
            {
                EditorGUILayout.LabelField("No established relationships.");
            }
            else
            {
                foreach (var kvp in bonds)
                {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), kvp.Value, $"{kvp.Key}: {kvp.Value:F2}");
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawCommunicationSection(Spirit spirit)
        {
            EditorGUILayout.LabelField("Communication", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            var intent = spirit.Communication.LatestIntent;
            if (intent.HasValue)
            {
                float age = Time.time - spirit.Communication.LastIntentTime;
                EditorGUILayout.LabelField("Latest Intent", $"{intent.Value.Priority} | {intent.Value.Topic}");
                EditorGUILayout.LabelField("Generated", $"{age:F1}s ago");
            }
            else
            {
                EditorGUILayout.LabelField("No intents generated yet.");
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawRuntimeStatusSection(Spirit spirit)
        {
            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUILayout.LabelField("Presence Mode", spirit.RuntimeData.CurrentPresenceMode.ToString());
            
            string layers = spirit.RuntimeData.ActiveBehavioralLayers.ToString();
            EditorGUILayout.LabelField("Active Layers", layers);
            
            EditorGUILayout.LabelField("Total Recent Memories", spirit.Memory.RecentMemory.Count.ToString());
            EditorGUILayout.LabelField("Total Active Concerns", spirit.Agency.ActiveConcerns.Count.ToString());
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void DrawDebugButtons(Spirit spirit)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Clear Memories"))
            {
                spirit.Memory.DebugClear();
            }
            if (GUILayout.Button("Clear Concerns"))
            {
                spirit.Agency.DebugClear();
            }
            if (GUILayout.Button("Reset Relationships"))
            {
                spirit.Relationships.DebugClear();
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Trigger Test Event (50 Damage)"))
            {
                SpiritManager.Instance.BroadcastEvent(new DamageEventData(50f, GameCode.Shared.DamageType.Physical));
            }
        }
    }
}
