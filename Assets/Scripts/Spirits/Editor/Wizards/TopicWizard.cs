using System.IO;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Wizards
{
    public class TopicWizard : ScriptableWizard
    {
        [Header("Topic Settings")]
        public string id = "NewTopic";
        public string displayName = "New Topic";
        public string category = "General";
        [TextArea(3, 6)]
        public string description = "";
        public Color editorColor = Color.white;

        [MenuItem("Spirits/Wizards/Create Topic", priority = 10)]
        public static void CreateWizard()
        {
            ScriptableWizard.DisplayWizard<TopicWizard>("Create New Topic", "Create");
        }

        private void OnWizardCreate()
        {
            TopicDefinition topic = ScriptableObject.CreateInstance<TopicDefinition>();
            
            // Set properties using SerializedObject to access private fields
            SerializedObject serializedObject = new SerializedObject(topic);
            serializedObject.FindProperty("id").stringValue = id;
            serializedObject.FindProperty("displayName").stringValue = displayName;
            serializedObject.FindProperty("category").stringValue = category;
            serializedObject.FindProperty("description").stringValue = description;
            serializedObject.FindProperty("editorColor").colorValue = editorColor;
            serializedObject.ApplyModifiedProperties();

            string directory = "Assets/Data/Spirits/Topics";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string path = $"{directory}/{id}.asset";
            AssetDatabase.CreateAsset(topic, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TopicWizard] Created Topic Definition at {path}");
            EditorGUIUtility.PingObject(topic);
        }
    }
}
