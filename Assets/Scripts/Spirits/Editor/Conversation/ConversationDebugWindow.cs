using GameCode.Spirits.Conversation.Data;
using GameCode.Spirits.Conversation.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Conversation
{
    /// <summary>
    /// Live debug EditorWindow for the Conversation System.
    /// Shows the active conversation state, history, cooldowns, and scoring
    /// information in real-time during Play Mode.
    /// </summary>
    public class ConversationDebugWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool showHistory = true;
        private bool showCooldowns = true;
        private bool showScoring = false;

        [MenuItem("Window/Spirits/Conversation Debug")]
        public static void ShowWindow()
        {
            GetWindow<ConversationDebugWindow>("Conversation Debug");
        }

        private void OnEnable()
        {
            // Repaint during Play Mode to keep the display live
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live conversation data.", MessageType.Info);
                return;
            }

            var director = ConversationDirector.Instance;
            if (director == null)
            {
                EditorGUILayout.HelpBox("ConversationDirector not found in the scene.", MessageType.Warning);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // ── Active Conversation ──
            DrawActiveConversation(director);

            EditorGUILayout.Space(8);

            // ── History ──
            showHistory = EditorGUILayout.Foldout(showHistory, "History", true, EditorStyles.foldoutHeader);
            if (showHistory)
            {
                DrawHistory(director);
            }

            EditorGUILayout.Space(4);

            // ── Manual Controls ──
            DrawManualControls(director);

            EditorGUILayout.EndScrollView();
        }

        private void DrawActiveConversation(ConversationDirector director)
        {
            EditorGUILayout.LabelField("Active Conversation", EditorStyles.boldLabel);

            var active = director.ActiveConversation;
            if (active == null)
            {
                EditorGUILayout.LabelField("  (idle — no active conversation)");
                return;
            }

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Asset", active.Asset.DisplayName);
            EditorGUILayout.LabelField("ID", active.Asset.Id);
            EditorGUILayout.LabelField("State", active.State.ToString());
            EditorGUILayout.LabelField("Current Node", active.CurrentNodeId.ToString());
            EditorGUILayout.LabelField("Nodes Played", $"{active.NodesPlayed} / {active.Asset.NodeCount}");
            EditorGUILayout.LabelField("Elapsed", $"{active.ElapsedTime:F1}s");

            // Current node details
            var currentNode = active.GetCurrentNode();
            if (currentNode != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Current Speaker", currentNode.Speaker.Value ?? "???");
                EditorGUILayout.LabelField("Line Key", currentNode.LineKey.Value ?? "(none)");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawHistory(ConversationDirector director)
        {
            var history = director.History;
            if (history == null || history.TotalRecords == 0)
            {
                EditorGUILayout.LabelField("  (no conversations recorded yet)");
                return;
            }

            EditorGUI.indentLevel++;

            var records = history.AllRecords;
            int count = Mathf.Min(records.Count, 15); // Show last 15

            for (int i = records.Count - 1; i >= records.Count - count; i--)
            {
                var record = records[i];
                Color stateColor = record.FinalState switch
                {
                    ConversationState.Completed => new Color(0.3f, 0.9f, 0.3f),
                    ConversationState.Cancelled => new Color(0.9f, 0.4f, 0.3f),
                    ConversationState.Interrupted => new Color(0.9f, 0.7f, 0.2f),
                    _ => Color.gray
                };

                EditorGUILayout.BeginHorizontal();

                // State indicator
                var stateStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = stateColor } };
                GUILayout.Label(record.FinalState.ToString(), stateStyle, GUILayout.Width(80));

                GUILayout.Label(record.ConversationId, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{record.TotalNodesPlayed} nodes", EditorStyles.miniLabel, GUILayout.Width(60));

                float ago = Time.time - record.EndTime;
                GUILayout.Label($"{ago:F0}s ago", EditorStyles.miniLabel, GUILayout.Width(60));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawManualControls(ConversationDirector director)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Manual Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Force Cancel", GUILayout.Height(24)))
            {
                director.ForceCancel();
            }

            if (director.ActiveConversation != null)
            {
                if (director.ActiveConversation.State == ConversationState.Paused)
                {
                    if (GUILayout.Button("Resume", GUILayout.Height(24)))
                    {
                        director.ResumeConversation();
                    }
                }
                else
                {
                    if (GUILayout.Button("Pause", GUILayout.Height(24)))
                    {
                        director.PauseConversation();
                    }
                }
            }

            if (GUILayout.Button("Purge Cooldowns", GUILayout.Height(24)))
            {
                director.PurgeCooldowns();
            }

            EditorGUILayout.EndHorizontal();

            // Manual trigger fire
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Fire Trigger:", GUILayout.Width(80));

            string[] quickTriggers = new string[]
            {
                "PlayerHPCritical", "PlayerHPLow", "PlayerDied", "BossAppears",
                "EnemyKilled", "PuzzleSolved", "PlayerIdle", "AmbientChatter",
                "SpiritSwapped", "SecretFound"
            };

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < quickTriggers.Length; i++)
            {
                if (GUILayout.Button(quickTriggers[i], EditorStyles.miniButton))
                {
                    director.OnTrigger(quickTriggers[i]);
                }

                if ((i + 1) % 5 == 0 && i < quickTriggers.Length - 1)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
