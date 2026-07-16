using System.IO;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Wizards
{
    public class SpiritCreationWizard : ScriptableWizard
    {
        [Header("Spirit Settings")]
        public string id = "Zenka";
        public string displayName = "Zenka";
        [TextArea(3, 6)]
        public string description = "A mysterious spirit.";

        [MenuItem("Spirits/Wizards/Create Spirit", priority = 1)]
        public static void CreateWizard()
        {
            ScriptableWizard.DisplayWizard<SpiritCreationWizard>("Create New Spirit", "Generate Full Package");
        }

        private void OnWizardCreate()
        {
            string rootDir = $"Assets/Data/Spirits/{id}";
            if (!Directory.Exists(rootDir))
            {
                Directory.CreateDirectory(rootDir);
                Directory.CreateDirectory($"{rootDir}/Dialogue");
                Directory.CreateDirectory($"{rootDir}/Portraits");
                Directory.CreateDirectory($"{rootDir}/Audio");
                Directory.CreateDirectory($"{rootDir}/Icons");
                Directory.CreateDirectory($"{rootDir}/Documentation");
            }

            // Generate Identity
            SpiritIdentityProfile identity = ScriptableObject.CreateInstance<SpiritIdentityProfile>();
            AssetDatabase.CreateAsset(identity, $"{rootDir}/Identity.asset");

            // Generate Communication
            SpiritCommunicationProfile communication = ScriptableObject.CreateInstance<SpiritCommunicationProfile>();
            AssetDatabase.CreateAsset(communication, $"{rootDir}/Communication.asset");

            // Generate Definition
            SpiritDefinition definition = ScriptableObject.CreateInstance<SpiritDefinition>();
            SerializedObject defSo = new SerializedObject(definition);
            
            // Wait, we should use SpiritId for the 'id' field now.
            // But we don't have direct access. Let's serialize it.
            SerializedProperty idProp = defSo.FindProperty("id");
            if (idProp != null)
            {
                idProp.FindPropertyRelative("Value").stringValue = id.ToLower();
            }
            
            defSo.FindProperty("displayName").stringValue = displayName;
            defSo.FindProperty("description").stringValue = description;
            defSo.FindProperty("identityProfile").objectReferenceValue = identity;
            defSo.FindProperty("communicationProfile").objectReferenceValue = communication;
            defSo.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(definition, $"{rootDir}/Definition.asset");

            // Generate Documentation README
            File.WriteAllText($"{rootDir}/Documentation/README.md", $"# {displayName}\n\n{description}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SpiritWizard] Successfully generated full package for {displayName} at {rootDir}");
            EditorGUIUtility.PingObject(definition);
        }
    }
}
