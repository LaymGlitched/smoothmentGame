using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Persistent editor preferences for NanoCollab.
    /// Accessed via Edit > Preferences > NanoCollab.
    /// </summary>
    public sealed class NanoCollabSettings : ScriptableSingleton<NanoCollabSettings>
    {
        [SerializeField] private string _displayName = "";
        [SerializeField] private int    _port        = 7420;
        [SerializeField] private bool   _enabled     = true;

        /// <summary>Display name shown to other collaborators. Defaults to OS username.</summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName)
                ? System.Environment.UserName
                : _displayName;
            set { _displayName = value; Save(true); }
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
                keywords  = new[] { "NanoCollab", "Collaboration", "Multiplayer", "Scene" }
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
            var newName = EditorGUILayout.TextField("Display Name", s._displayName);
            if (string.IsNullOrWhiteSpace(newName))
                EditorGUILayout.HelpBox(
                    $"Defaults to OS username: {System.Environment.UserName}",
                    MessageType.Info);

            var newPort = EditorGUILayout.IntField("Port", s._port);

            if (EditorGUI.EndChangeCheck())
            {
                s._displayName = newName;
                s._port        = Mathf.Clamp(newPort, 1024, 65535);
                s.Save(true);
            }
        }
    }
}
