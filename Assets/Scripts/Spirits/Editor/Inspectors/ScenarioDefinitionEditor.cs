using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Inspectors
{
    [CustomEditor(typeof(ScenarioDefinition))]
    public class ScenarioDefinitionEditor : UnityEditor.Editor
    {
        private GUIStyle nodeStyle;
        private GUIStyle arrowStyle;
        
        public override void OnInspectorGUI()
        {
            if (nodeStyle == null)
            {
                nodeStyle = new GUIStyle(EditorStyles.helpBox);
                nodeStyle.alignment = TextAnchor.MiddleCenter;
                nodeStyle.fontSize = 12;
                nodeStyle.fontStyle = FontStyle.Bold;
                nodeStyle.normal.textColor = Color.white;
                nodeStyle.margin = new RectOffset(20, 20, 5, 5);
                nodeStyle.padding = new RectOffset(10, 10, 10, 10);
            }

            if (arrowStyle == null)
            {
                arrowStyle = new GUIStyle(EditorStyles.label);
                arrowStyle.alignment = TextAnchor.MiddleCenter;
                arrowStyle.fontSize = 18;
                arrowStyle.fontStyle = FontStyle.Bold;
            }

            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scenario Flow", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Trigger Event
            GUILayout.BeginVertical(nodeStyle);
            EditorGUILayout.LabelField("WHEN", EditorStyles.centeredGreyMiniLabel);
            SerializedProperty triggerProp = serializedObject.FindProperty("triggerEvent");
            EditorGUILayout.PropertyField(triggerProp, GUIContent.none);
            GUILayout.EndVertical();

            // Flow Arrow
            DrawArrow();

            // Conditions
            SerializedProperty conditionsProp = serializedObject.FindProperty("conditionGroups");
            if (conditionsProp.arraySize == 0)
            {
                GUILayout.BeginVertical(nodeStyle);
                EditorGUILayout.LabelField("ALWAYS (No Conditions)", EditorStyles.centeredGreyMiniLabel);
                GUILayout.EndVertical();
                DrawArrow();
            }
            else
            {
                for (int i = 0; i < conditionsProp.arraySize; i++)
                {
                    GUILayout.BeginVertical(nodeStyle);
                    string prefix = i == 0 ? "IF" : "AND / OR";
                    EditorGUILayout.LabelField(prefix, EditorStyles.centeredGreyMiniLabel);
                    
                    SerializedProperty groupProp = conditionsProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(groupProp, true); // True to draw children (the SerializeReference list)
                    
                    GUILayout.EndVertical();
                    DrawArrow();
                }
            }
            
            // Add Condition Button
            if (GUILayout.Button("Add Condition Group"))
            {
                conditionsProp.arraySize++;
            }
            EditorGUILayout.Space();

            // Outcomes
            GUILayout.BeginVertical(nodeStyle);
            EditorGUILayout.LabelField("THEN", EditorStyles.centeredGreyMiniLabel);
            SerializedProperty outcomesProp = serializedObject.FindProperty("resultingTopics");
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(outcomesProp, new GUIContent("Resulting Topics"), true);
            EditorGUI.indentLevel--;
            GUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawArrow()
        {
            EditorGUILayout.LabelField("↓", arrowStyle);
        }
    }
}
