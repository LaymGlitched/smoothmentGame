using Reiteki.Localization.Core;
using UnityEditor;
using UnityEngine;

namespace Reiteki.Localization.Editor
{
    [CustomPropertyDrawer(typeof(LocalizationKey))]
    public class LocalizationKeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("Value");
            
            // For now, draw as a standard string field. 
            // In the future, this can be expanded to parse JSON files in the project 
            // and provide a dropdown of available localization keys.
            
            EditorGUI.BeginProperty(position, label, property);
            
            // Add a small prefix or icon area if desired
            Rect textRect = new Rect(position.x, position.y, position.width, position.height);
            valueProp.stringValue = EditorGUI.TextField(textRect, label, valueProp.stringValue);
            
            EditorGUI.EndProperty();
        }
    }
}
