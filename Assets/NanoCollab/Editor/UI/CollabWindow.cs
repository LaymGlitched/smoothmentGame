using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Lightweight EditorWindow showing connection status, connected users,
    /// and basic settings. Accessible via Window > NanoCollab.
    /// </summary>
    public sealed class CollabWindow : EditorWindow
    {
        private static SessionManager _session;
        private Vector2 _scrollPos;

        [MenuItem("Window/NanoCollab")]
        private static void ShowWindow()
        {
            var win = GetWindow<CollabWindow>("NanoCollab");
            win.minSize = new Vector2(240, 200);
        }

        /// <summary>Called by SessionManager to provide the reference.</summary>
        public static void Bind(SessionManager session)
        {
            _session = session;
        }

        private void OnEnable()
        {
            // Repaint periodically for live status
            EditorApplication.update += RepaintTick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintTick;
        }

        private float _lastRepaint;
        private void RepaintTick()
        {
            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastRepaint > 0.5f)
            {
                _lastRepaint = now;
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (_session == null)
            {
                EditorGUILayout.HelpBox(
                    "NanoCollab is not active. Make sure it is enabled in Edit > Preferences > NanoCollab.",
                    MessageType.Info);
                return;
            }

            DrawHeader();
            EditorGUILayout.Space(4);
            DrawUserList();
            GUILayout.FlexibleSpace();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Status indicator
            var state = _session.State;
            string statusText;
            Color statusColor;

            switch (state)
            {
                case SessionManager.SessionState.Connected:
                    statusText  = "● Connected";
                    statusColor = new Color(0.4f, 0.87f, 0.47f);
                    break;
                case SessionManager.SessionState.Hosting:
                    statusText  = "● Hosting";
                    statusColor = new Color(0.33f, 0.69f, 1.0f);
                    break;
                case SessionManager.SessionState.Discovering:
                    statusText  = "◌ Discovering…";
                    statusColor = new Color(1f, 0.8f, 0.27f);
                    break;
                default:
                    statusText  = "○ Idle";
                    statusColor = Color.gray;
                    break;
            }

            var oldColor = GUI.contentColor;
            GUI.contentColor = statusColor;
            GUILayout.Label(statusText, EditorStyles.boldLabel);
            GUI.contentColor = oldColor;

            GUILayout.FlexibleSpace();

            // Scene name
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            GUILayout.Label(sceneName, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawUserList()
        {
            var presence = _session.Presence;
            if (presence == null || presence.Users.Count == 0)
            {
                EditorGUILayout.LabelField("No collaborators connected.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EditorGUILayout.LabelField("Collaborators", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var kv in presence.Users)
            {
                var user = kv.Value;
                EditorGUILayout.BeginHorizontal();

                // Color dot
                var dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                dotRect.y += 2;
                EditorGUI.DrawRect(dotRect, user.Color);

                // Name
                EditorGUILayout.LabelField(user.Name, GUILayout.MinWidth(80));

                // Latency
                if (user.LatencyMs > 0)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        $"{user.LatencyMs:F0}ms",
                        EditorStyles.miniLabel,
                        GUILayout.Width(50));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var settings = NanoCollabSettings.instance;
            GUILayout.Label($"You: {settings.DisplayName}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            var peerCount = _session.Transport != null ? _session.Transport.PeerCount : 0;
            GUILayout.Label($"Peers: {peerCount}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }
    }
}
