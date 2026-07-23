using System;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Lightweight EditorWindow showing connection status, connected users,
    /// latency, and Right-Click 'Follow Camera' option.
    /// Accessible via Window > NanoCollab.
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

        public static void Bind(SessionManager session)
        {
            _session = session;
        }

        private void OnEnable()
        {
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
                    "NanoCollab is inactive. Enable it in Edit > Preferences > NanoCollab.",
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

            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            GUILayout.Label(sceneName, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawUserList()
        {
            var presence = _session.Presence;
            if (presence == null || presence.Users.Count == 0)
            {
                EditorGUILayout.LabelField("No collaborators connected.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EditorGUILayout.LabelField("Collaborators (Right-Click to Follow)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            var currentFollowId = _session.CameraSync != null ? _session.CameraSync.FollowUserId : null;

            foreach (var kv in presence.Users)
            {
                var user = kv.Value;
                var rowRect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Color dot
                var dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                dotRect.y += 2;
                EditorGUI.DrawRect(dotRect, user.Color);

                // Name & Follow indicator
                string displayName = user.Name;
                if (currentFollowId.HasValue && currentFollowId.Value == user.Id)
                    displayName += " [Following]";

                EditorGUILayout.LabelField(displayName, EditorStyles.label, GUILayout.MinWidth(100));

                if (user.LatencyMs > 0)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"{user.LatencyMs:F0}ms", EditorStyles.miniLabel, GUILayout.Width(45));
                }

                EditorGUILayout.EndHorizontal();

                // Context Menu on Right Click
                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    bool isFollowing = currentFollowId.HasValue && currentFollowId.Value == user.Id;

                    if (isFollowing)
                    {
                        menu.AddItem(new GUIContent("Stop Following Camera"), false, () =>
                        {
                            _session.CameraSync?.SetFollowUser(null);
                        });
                    }
                    else
                    {
                        menu.AddItem(new GUIContent($"Follow Camera ({user.Name})"), false, () =>
                        {
                            _session.CameraSync?.SetFollowUser(user.Id);
                        });
                    }

                    menu.ShowAsContext();
                    Event.current.Use();
                }
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
