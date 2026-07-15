using System.Linq;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(ConcernId))]
    public class ConcernIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            string currentValue = valueProp.stringValue;

            // Concerns map directly to TopicDefinitions conceptually
            string[] guids = AssetDatabase.FindAssets("t:TopicDefinition");
            var topics = guids.Select(g => AssetDatabase.LoadAssetAtPath<TopicDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                              .Where(t => t != null)
                              .OrderBy(t => t.Category)
                              .ThenBy(t => t.DisplayName)
                              .ToList();

            if (topics.Count == 0)
            {
                EditorGUI.PropertyField(position, valueProp, label);
                return;
            }

            GUIContent[] options = new GUIContent[topics.Count + 1];
            options[0] = new GUIContent("None");
            int selectedIndex = 0;

            for (int i = 0; i < topics.Count; i++)
            {
                options[i + 1] = new GUIContent($"{topics[i].Category}/{topics[i].DisplayName} ({topics[i].Id.Value})");
                if (currentValue == topics[i].Id.Value)
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
                    valueProp.stringValue = topics[newIndex - 1].Id.Value;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
