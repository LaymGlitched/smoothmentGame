using AdvancedPrefabPainter.Runtime.Data;
using AdvancedPrefabPainter.Runtime.Rendering;
using UnityEditor;
using UnityEngine;

namespace AdvancedPrefabPainter.Editor.Core
{
    public class PrefabPainterWindow : EditorWindow
    {
        private ScenePainter scenePainter;

        private BiomeProfile activeProfile;
        private InstancedPainterData targetData;
        private PaintMode currentMode = PaintMode.Select;

        private Vector2 scrollPos;

        [MenuItem("Tools/Advanced Prefab Painter")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabPainterWindow>("Prefab Painter");
            window.Show();
        }

        private void OnEnable()
        {
            scenePainter = new ScenePainter();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (scenePainter != null)
            {
                scenePainter.IsActive = false;
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (scenePainter != null)
            {
                scenePainter.OnSceneGUI(sceneView);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Advanced Prefab Painter", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            targetData = (InstancedPainterData)EditorGUILayout.ObjectField("Target Data", targetData, typeof(InstancedPainterData), true);
            activeProfile = (BiomeProfile)EditorGUILayout.ObjectField("Biome Profile", activeProfile, typeof(BiomeProfile), false);

            EditorGUILayout.Space();
            
            GUILayout.BeginHorizontal();
            GUI.color = currentMode == PaintMode.Paint ? Color.green : Color.white;
            if (GUILayout.Button("Paint", GUILayout.Height(30))) SetMode(PaintMode.Paint);
            
            GUI.color = currentMode == PaintMode.Erase ? Color.red : Color.white;
            if (GUILayout.Button("Erase", GUILayout.Height(30))) SetMode(PaintMode.Erase);
            
            GUI.color = currentMode == PaintMode.Select ? Color.cyan : Color.white;
            if (GUILayout.Button("Select", GUILayout.Height(30))) SetMode(PaintMode.Select);
            
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                UpdateScenePainter();
            }

            if (targetData == null)
            {
                EditorGUILayout.HelpBox("Assign a Target Data (InstancedPainterData) component from the scene.", MessageType.Warning);
                if (GUILayout.Button("Create Target Data in Scene"))
                {
                    GameObject go = new GameObject("Painted Instances");
                    targetData = go.AddComponent<InstancedPainterData>();
                    UpdateScenePainter();
                }
            }

            if (activeProfile == null)
            {
                EditorGUILayout.HelpBox("Assign a Biome Profile to start painting.", MessageType.Warning);
            }
            else
            {
                DrawProfileSettings();
            }
        }

        private void DrawProfileSettings()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            if (activeProfile.brushSettings != null)
            {
                var brushObj = new SerializedObject(activeProfile.brushSettings);
                brushObj.Update();
                
                SerializedProperty prop = brushObj.GetIterator();
                if (prop.NextVisible(true))
                {
                    while (prop.NextVisible(false))
                    {
                        EditorGUILayout.PropertyField(prop, true);
                    }
                }
                brushObj.ApplyModifiedProperties();
            }
            else
            {
                EditorGUILayout.HelpBox("Biome Profile is missing Brush Settings.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
            if (activeProfile.prefabs != null)
            {
                var prefabsObj = new SerializedObject(activeProfile.prefabs);
                prefabsObj.Update();
                EditorGUILayout.PropertyField(prefabsObj.FindProperty("placementMode"));
                EditorGUILayout.PropertyField(prefabsObj.FindProperty("items"), true);
                prefabsObj.ApplyModifiedProperties();
            }
            else
            {
                EditorGUILayout.HelpBox("Biome Profile is missing Prefabs.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        private void SetMode(PaintMode mode)
        {
            currentMode = mode;
            UpdateScenePainter();
            SceneView.RepaintAll();
        }

        private void UpdateScenePainter()
        {
            if (scenePainter != null)
            {
                scenePainter.IsActive = currentMode != PaintMode.Select;
                scenePainter.CurrentMode = currentMode;
                scenePainter.ActiveProfile = activeProfile;
                scenePainter.TargetData = targetData;

                if (scenePainter.IsActive && targetData != null)
                {
                    scenePainter.RefreshHash();
                }
            }
        }
    }
}
