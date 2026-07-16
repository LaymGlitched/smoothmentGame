using System.IO;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Wizards
{
    public class EventWizard : ScriptableWizard
    {
        [Header("Event Settings")]
        public string id = "PlayerLoadedGame";
        public string displayName = "Player Loaded Game";
        public string category = "Gameplay";
        [TextArea(3, 6)]
        public string description = "Triggered once when the save finishes loading.";

        [MenuItem("Spirits/Wizards/Create Event", priority = 11)]
        public static void CreateWizard()
        {
            ScriptableWizard.DisplayWizard<EventWizard>("Create New Event", "Create");
        }

        private void OnWizardCreate()
        {
            GameplayEventDefinition ev = ScriptableObject.CreateInstance<GameplayEventDefinition>();
            
            // Set properties using SerializedObject to access private fields
            SerializedObject serializedObject = new SerializedObject(ev);
            serializedObject.FindProperty("id").stringValue = id;
            serializedObject.FindProperty("displayName").stringValue = displayName;
            serializedObject.FindProperty("category").stringValue = category;
            serializedObject.FindProperty("description").stringValue = description;
            serializedObject.ApplyModifiedProperties();

            string directory = "Assets/Data/Spirits/Events";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string path = $"{directory}/{id}.asset";
            AssetDatabase.CreateAsset(ev, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[EventWizard] Created Event Definition at {path}");
            EditorGUIUtility.PingObject(ev);
        }
    }
}
