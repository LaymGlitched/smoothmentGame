using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MaintainTransform))]
[CanEditMultipleObjects]
public class MaintainTransformEditor : Editor
{
    private SerializedProperty useWorldSpaceProp;

    private SerializedProperty targetPositionProp;
    private SerializedProperty targetRotationProp;
    private SerializedProperty targetScaleProp;

    private SerializedProperty targetPosXProp;
    private SerializedProperty targetPosYProp;
    private SerializedProperty targetPosZProp;

    private SerializedProperty targetRotXProp;
    private SerializedProperty targetRotYProp;
    private SerializedProperty targetRotZProp;

    private SerializedProperty targetScaleXProp;
    private SerializedProperty targetScaleYProp;
    private SerializedProperty targetScaleZProp;

    private void OnEnable()
    {
        useWorldSpaceProp = serializedObject.FindProperty("useWorldSpace");

        targetPositionProp = serializedObject.FindProperty("targetPosition");
        targetRotationProp = serializedObject.FindProperty("targetRotation");
        targetScaleProp = serializedObject.FindProperty("targetScale");

        targetPosXProp = serializedObject.FindProperty("targetPosX");
        targetPosYProp = serializedObject.FindProperty("targetPosY");
        targetPosZProp = serializedObject.FindProperty("targetPosZ");

        targetRotXProp = serializedObject.FindProperty("targetRotX");
        targetRotYProp = serializedObject.FindProperty("targetRotY");
        targetRotZProp = serializedObject.FindProperty("targetRotZ");

        targetScaleXProp = serializedObject.FindProperty("targetScaleX");
        targetScaleYProp = serializedObject.FindProperty("targetScaleY");
        targetScaleZProp = serializedObject.FindProperty("targetScaleZ");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(useWorldSpaceProp, new GUIContent("Use World Space"));

        EditorGUILayout.Space();

        // Draw standard Vector3 target properties
        EditorGUILayout.PropertyField(targetPositionProp, new GUIContent("Target Position"));
        EditorGUILayout.PropertyField(targetRotationProp, new GUIContent("Target Rotation"));
        EditorGUILayout.PropertyField(targetScaleProp, new GUIContent("Target Scale"));

        EditorGUILayout.Space();

        // Draw horizontal toggle matrix for target axes
        DrawToggleGroup("Position Targets", targetPosXProp, targetPosYProp, targetPosZProp);
        DrawToggleGroup("Rotation Targets", targetRotXProp, targetRotYProp, targetRotZProp);
        DrawToggleGroup("Scale Targets", targetScaleXProp, targetScaleYProp, targetScaleZProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawToggleGroup(string labelText, SerializedProperty x, SerializedProperty y, SerializedProperty z)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.PrefixLabel(labelText);

        x.boolValue = EditorGUILayout.ToggleLeft("X", x.boolValue, GUILayout.Width(35f));
        y.boolValue = EditorGUILayout.ToggleLeft("Y", y.boolValue, GUILayout.Width(35f));
        z.boolValue = EditorGUILayout.ToggleLeft("Z", z.boolValue, GUILayout.Width(35f));

        EditorGUILayout.EndHorizontal();
    }
}


