using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Persistent editor preferences for NanoCollab.
    /// Saved permanently to UserSettings/NanoCollabSettings.asset via ScriptableSingleton.
    /// </summary>
    [FilePath("UserSettings/NanoCollabSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class NanoCollabSettings : ScriptableSingleton<NanoCollabSettings>
    {
        [SerializeField] private string _displayName = "";
        [SerializeField] private Color  _userColor   = new Color(0.33f, 0.69f, 1.00f); // Default Sky Blue
        [SerializeField] private int    _port        = 7420;
        [SerializeField] private bool   _enabled     = true;

        /// <summary>Display name shown to other collaborators. Defaults to OS username.</summary>
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_displayName))
                {
                    _displayName = System.Environment.UserName;
                    if (string.IsNullOrWhiteSpace(_displayName))
                        _displayName = "Developer_" + Random.Range(100, 999);
                }
                return _displayName;
            }
            set
            {
                _displayName = value;
                Save(true);
            }
        }

        /// <summary>User presence color shown in SceneView gizmos and user list.</summary>
        public Color UserColor
        {
            get
            {
                if (_userColor.a < 0.1f)
                    _userColor = new Color(0.33f, 0.69f, 1.00f);
                return _userColor;
            }
            set
            {
                _userColor = value;
                Save(true);
            }
        }

        /// <summary>UDP discovery and TCP listen port.</summary>
        public int Port
        {
            get => _port;
            set { _port = Mathf.Clamp(value, 1024, 65535); Save(true); }
        }

        /// <summary>Master enable/disable toggle.</summary>
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; Save(true); }
        }

        // --- Preferences UI ---

        [SettingsProvider]
        private static SettingsProvider CreatePreferencesProvider()
        {
            return new SettingsProvider("Preferences/NanoCollab", SettingsScope.User)
            {
                label = "NanoCollab",
                guiHandler = _ => DrawPreferencesGUI(),
                keywords  = new[] { "NanoCollab", "Collaboration", "Multiplayer", "Scene", "Color" }
            };
        }

        private static void DrawPreferencesGUI()
        {
            var s = instance;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("NanoCollab Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();

            s._enabled = EditorGUILayout.Toggle("Enabled", s._enabled);

            EditorGUILayout.Space(4);
            var newName  = EditorGUILayout.TextField("Display Name", s.DisplayName);
            var newColor = EditorGUILayout.ColorField("User Color", s.UserColor);

            if (string.IsNullOrWhiteSpace(newName))
                EditorGUILayout.HelpBox(
                    $"Defaults to OS username: {System.Environment.UserName}",
                    MessageType.Info);

            var newPort = EditorGUILayout.IntField("Port", s._port);

            if (EditorGUI.EndChangeCheck())
            {
                s._displayName = newName;
                s._userColor   = newColor;
                s._port        = Mathf.Clamp(newPort, 1024, 65535);
                s.Save(true);
            }
        }
    }
}
