#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace StylizedGrassSystem
{
    [CustomEditor(typeof(GrassPainter))]
    public class GrassPainterEditor : Editor
    {
        private bool isPainting = false;
        private float brushSize = 5f;
        private float brushDensity = 50f;
        
        // Foldouts
        private bool showReferences = true;
        private bool showPainting = true;
        private bool showSettings = true;

        public override void OnInspectorGUI()
        {
            GrassPainter painter = (GrassPainter)target;

            serializedObject.Update();

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 13;
            headerStyle.margin = new RectOffset(0, 0, 10, 5);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Stylized Grass Painter", headerStyle);
            EditorGUILayout.HelpBox("Paint grass directly on colliders in the Scene View.", MessageType.Info);
            EditorGUILayout.Space(5);

            // References
            showReferences = EditorGUILayout.Foldout(showReferences, "References", true, EditorStyles.foldoutHeader);
            if (showReferences)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("grassCullingCompute"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("grassMaterial"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("grassBladeMesh"));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);

            // Paint Tools
            showPainting = EditorGUILayout.Foldout(showPainting, "Paint Tools", true, EditorStyles.foldoutHeader);
            if (showPainting)
            {
                EditorGUI.indentLevel++;
                
                GUI.backgroundColor = isPainting ? Color.green : Color.white;
                if (GUILayout.Button(isPainting ? "Stop Painting" : "Start Painting (Scene View)", GUILayout.Height(30)))
                {
                    isPainting = !isPainting;
                }
                GUI.backgroundColor = Color.white;

                if (isPainting)
                {
                    EditorGUILayout.HelpBox("Left Click & Drag to Paint.\nShift + Left Click to Erase.", MessageType.Info);
                }

                brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.5f, 50f);
                brushDensity = EditorGUILayout.Slider("Brush Density", brushDensity, 10f, 1000f);
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Clear All Grass Patches"))
                {
                    if (EditorUtility.DisplayDialog("Clear All Grass", "Are you sure you want to delete all painted grass?", "Yes", "No"))
                    {
                        painter.ClearAll();
                        EditorUtility.SetDirty(painter);
                    }
                }
                
                EditorGUILayout.LabelField($"Total Patches: {painter.patches.Count}");
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);

            // Settings
            showSettings = EditorGUILayout.Foldout(showSettings, "Settings & Colors", true, EditorStyles.foldoutHeader);
            if (showSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("globalDensityMultiplier"), new GUIContent("Global Density"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("heightOffset"), new GUIContent("Ground Offset"));
                
                SerializedProperty sizeRange = serializedObject.FindProperty("sizeRange");
                Vector2 size = sizeRange.vector2Value;
                EditorGUILayout.MinMaxSlider("Width Range", ref size.x, ref size.y, 0.1f, 3.0f);
                sizeRange.vector2Value = size;
                
                SerializedProperty heightRange = serializedObject.FindProperty("heightRange");
                Vector2 height = heightRange.vector2Value;
                EditorGUILayout.MinMaxSlider("Height Range", ref height.x, ref height.y, 0.1f, 5.0f);
                heightRange.vector2Value = height;

                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("colorVariation1"), new GUIContent("Color Variation A"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("colorVariation2"), new GUIContent("Color Variation B"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cullDistance"), new GUIContent("Max Render Distance"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lodStartRatio"), new GUIContent("LOD Start (lower = faster)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("castShadows"), new GUIContent("Cast Shadows (Expensive!)"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            if (!isPainting) return;

            GrassPainter painter = (GrassPainter)target;

            // Prevent selecting other objects while painting
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Draw brush cursor
                Handles.color = e.shift ? new Color(1f, 0f, 0f, 0.5f) : new Color(0f, 1f, 0f, 0.5f);
                Handles.DrawSolidDisc(hit.point, hit.normal, brushSize);
                Handles.color = Color.white;
                Handles.DrawWireDisc(hit.point, hit.normal, brushSize);

                if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                {
                    if (e.button == 0) // Left click
                    {
                        if (e.shift)
                        {
                            // Erase
                            painter.ErasePatches(hit.point, brushSize);
                        }
                        else
                        {
                            // Paint
                            // Check if we already have a patch very close to avoid overwhelming density
                            bool tooClose = false;
                            foreach (var p in painter.patches)
                            {
                                if (Vector3.Distance(p.positionWS, hit.point) < brushSize * 0.5f)
                                {
                                    tooClose = true;
                                    break;
                                }
                            }

                            if (!tooClose)
                            {
                                painter.AddPatch(hit.point, hit.normal, brushSize, brushDensity);
                                EditorUtility.SetDirty(painter);
                            }
                        }
                        e.Use();
                    }
                }
            }
            
            // Force redraw so the brush circle follows mouse smoothly
            SceneView.RepaintAll();
        }
    }
}
#endif
