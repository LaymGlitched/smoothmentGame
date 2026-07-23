using System.Linq;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(EventId))]
    public class EventIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            string currentValue = valueProp.stringValue;

            // Retrieve cached GameplayEventDefinition assets (fast O(1) memory lookup)
            var events = EditorAssetCache<GameplayEventDefinition>.GetAssets()
                .OrderBy(e => e.Category)
                .ThenBy(e => e.DisplayName)
                .ToList();

            if (events.Count == 0)
            {
                EditorGUI.PropertyField(position, valueProp, label);
                return;
            }

            GUIContent[] options = new GUIContent[events.Count + 1];
            options[0] = new GUIContent("None");
            int selectedIndex = 0;

            for (int i = 0; i < events.Count; i++)
            {
                options[i + 1] = new GUIContent($"{events[i].Category}/{events[i].DisplayName} ({events[i].Id.Value})");
                if (currentValue == events[i].Id.Value)
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
                    valueProp.stringValue = events[newIndex - 1].Id.Value;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
