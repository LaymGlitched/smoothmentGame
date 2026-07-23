using System.Linq;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(SpiritId))]
    public class SpiritIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            string currentValue = valueProp.stringValue;

            // Retrieve cached SpiritDefinition assets (fast O(1) memory lookup)
            var spirits = EditorAssetCache<SpiritDefinition>.GetAssets()
                .OrderBy(s => s.DisplayName)
                .ToList();

            if (spirits.Count == 0)
            {
                EditorGUI.PropertyField(position, valueProp, label);
                return;
            }

            GUIContent[] options = new GUIContent[spirits.Count + 1];
            options[0] = new GUIContent("None");
            int selectedIndex = 0;

            for (int i = 0; i < spirits.Count; i++)
            {
                options[i + 1] = new GUIContent($"{spirits[i].DisplayName} ({spirits[i].Id.Value})");
                if (currentValue == spirits[i].Id.Value)
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
                    valueProp.stringValue = spirits[newIndex - 1].Id.Value;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
