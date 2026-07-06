using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FloatingIslandGenerator))]
public class FloatingIslandGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FloatingIslandGenerator gen = (FloatingIslandGenerator)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                Undo.RecordObject(gen, "Generate Island");
                gen.Generate();
                EditorUtility.SetDirty(gen);
            }

            if (GUILayout.Button("New Random Seed + Generate", GUILayout.Height(30)))
            {
                Undo.RecordObject(gen, "Randomize Island Seed");
                gen.seed = Random.Range(int.MinValue, int.MaxValue);
                gen.Generate();
                EditorUtility.SetDirty(gen);
            }
        }

        EditorGUILayout.HelpBox(
            "Tip: keep 'Randomize Seed On Generate' off if you want to fine-tune sliders " +
            "on the current seed. Turn it on to explore new island shapes each click.",
            MessageType.Info);
    }
}