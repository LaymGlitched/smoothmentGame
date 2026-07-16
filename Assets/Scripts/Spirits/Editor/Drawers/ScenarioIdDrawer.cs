using System.Linq;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(ScenarioId))]
    public class ScenarioIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            string currentValue = valueProp.stringValue;

            // Find all ScenarioDefinition assets
            string[] guids = AssetDatabase.FindAssets("t:ScenarioDefinition");
            var scenarios = guids.Select(g => AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                                 .Where(s => s != null)
                                 .OrderBy(s => s.Id.Value)
                                 .ToList();

            if (scenarios.Count == 0)
            {
                EditorGUI.PropertyField(position, valueProp, label);
                return;
            }

            GUIContent[] options = new GUIContent[scenarios.Count + 1];
            options[0] = new GUIContent("None");
            int selectedIndex = 0;

            for (int i = 0; i < scenarios.Count; i++)
            {
                options[i + 1] = new GUIContent($"{scenarios[i].Id.Value}");
                if (currentValue == scenarios[i].Id.Value)
                {
                    selectedIndex = i + 1;
                }
            }

            EditorGUI.BeginProperty(position, label, property);
            
            int newIndex = EditorGUI.Popup(position, label, selectedIndex, options);
            if (newIndex != selectedIndex)
            {
                if (newIndex == 0)
                {
                    valueProp.stringValue = string.Empty;
                }
                else
                {
                    valueProp.stringValue = scenarios[newIndex - 1].Id.Value;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
