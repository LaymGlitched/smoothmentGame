using System;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Editor window for NanoCollab.
    /// Displays connection status, active collaborator list with color swatches & latency,
    /// camera follow toggle, direct IP join controls, and identity customization.
    /// </summary>
    public sealed class CollabWindow : EditorWindow
    {
        private static SessionManager _session;
        private Vector2 _scrollPos;
        private bool _showDirectConnect;
        private bool _showProfile = true;
        private string _directIpInput = "";

        [MenuItem("Window/NanoCollab", false, 2050)]
        public static void Open()
        {
            var win = GetWindow<CollabWindow>("NanoCollab");
            win.minSize = new Vector2(280, 360);
            win.Show();
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
            DrawProfileSection();
            EditorGUILayout.Space(4);
            DrawUserList();
            DrawDirectConnectSection();
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
                    if (_session.Transport != null && _session.Transport.IsConnecting)
                        statusText = "◌ Connecting…";
                    else
                        statusText = "◌ Discovering…";
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

        private void DrawProfileSection()
        {
            var settings = NanoCollabSettings.instance;

            _showProfile = EditorGUILayout.Foldout(_showProfile, "My Identity (Name & Color)", true);
            if (_showProfile)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();
                string newName  = EditorGUILayout.TextField("Display Name", settings.DisplayName);
                Color  newColor = EditorGUILayout.ColorField("Avatar Color", settings.UserColor);

                if (EditorGUI.EndChangeCheck())
                {
                    settings.DisplayName = newName;
                    settings.UserColor   = newColor;

                    if (_session != null)
                    {
                        _session.Presence.AddUser(_session.LocalId, newName, customColor: newColor);
                        _session.BroadcastLocalUserJoin();
                    }
                }

                EditorGUILayout.EndVertical();
            }
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

                var dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                dotRect.y += 2;
                EditorGUI.DrawRect(dotRect, user.Color);

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

            DrawBotSection();
        }

        private void DrawBotSection()
        {
            if (_session?.Bot == null) return;

            EditorGUILayout.Space(6);
            bool isBotActive = _session.Bot.IsActive;

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = isBotActive ? new Color(1f, 0.4f, 0.4f) : new Color(0.6f, 0.9f, 0.6f);

            string btnText = isBotActive ? "Despawn Simulated Bot" : "🤖 Spawn Test Bot (Single-Editor)";
            if (GUILayout.Button(btnText, GUILayout.Height(24)))
            {
                _session.Bot.ToggleBot();
            }

            GUI.backgroundColor = oldBg;
        }

        private void DrawDirectConnectSection()
        {
            EditorGUILayout.Space(4);
            _showDirectConnect = EditorGUILayout.Foldout(_showDirectConnect, "Direct LAN IP Join", true);
            if (_showDirectConnect)
            {
                EditorGUILayout.BeginHorizontal();
                _directIpInput = EditorGUILayout.TextField(_directIpInput, GUILayout.MinWidth(120));
                if (GUILayout.Button("Connect IP", GUILayout.Width(80)))
                {
                    if (!string.IsNullOrWhiteSpace(_directIpInput))
                    {
                        _session.ConnectDirect(_directIpInput);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
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
