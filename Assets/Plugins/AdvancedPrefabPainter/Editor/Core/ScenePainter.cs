using AdvancedPrefabPainter.Runtime.Data;
using AdvancedPrefabPainter.Runtime.Rendering;
using UnityEditor;
using UnityEngine;

namespace AdvancedPrefabPainter.Editor.Core
{
    public enum PaintMode
    {
        Paint,
        Erase,
        Select
    }

    public class ScenePainter
    {
        public bool IsActive { get; set; }
        public PaintMode CurrentMode { get; set; }
        public BiomeProfile ActiveProfile { get; set; }
        public InstancedPainterData TargetData { get; set; }

        private PlacementEngine placementEngine;
        private bool isPainting = false;

        public ScenePainter()
        {
            placementEngine = new PlacementEngine();
        }

        public void RefreshHash()
        {
            placementEngine.RegisterExistingPoints(TargetData, ActiveProfile);
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (!IsActive || ActiveProfile == null || ActiveProfile.brushSettings == null || TargetData == null) return;
            if (CurrentMode == PaintMode.Select) return;

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            // Raycast for brush position
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var sample = Algorithms.SurfaceSampler.SampleSurface(ray.origin, ray.direction, 1000f, ActiveProfile.brushSettings.hitMask);

            if (sample.isValid)
            {
                DrawBrushPreview(sample.point, sample.normal, ActiveProfile.brushSettings.radius);

                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    isPainting = true;
                    GUIUtility.hotControl = controlID;
                    e.Use();
                    
                    UndoManager.RecordPaintAction(TargetData, CurrentMode == PaintMode.Paint ? "Paint Objects" : "Erase Objects");
                    ApplyStroke(sample.point, sample.normal);
                }
                else if (e.type == EventType.MouseDrag && e.button == 0 && isPainting)
                {
                    e.Use();
                    ApplyStroke(sample.point, sample.normal);
                }
                else if (e.type == EventType.MouseUp && e.button == 0)
                {
                    if (isPainting)
                    {
                        isPainting = false;
                        GUIUtility.hotControl = 0;
                        UndoManager.SetDirty(TargetData);
                        e.Use();
                    }
                }
            }

            // Prevent selection changes when painting
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlID);
            }
        }

        private void DrawBrushPreview(Vector3 center, Vector3 normal, float radius)
        {
            Handles.color = CurrentMode == PaintMode.Paint ? new Color(0, 1, 0, 0.2f) : new Color(1, 0, 0, 0.2f);
            Handles.DrawSolidDisc(center, normal, radius);
            Handles.color = CurrentMode == PaintMode.Paint ? Color.green : Color.red;
            Handles.DrawWireDisc(center, normal, radius);
        }

        private void ApplyStroke(Vector3 center, Vector3 normal)
        {
            if (CurrentMode == PaintMode.Paint)
            {
                placementEngine.PaintStroke(center, normal, ActiveProfile, TargetData);
            }
            else if (CurrentMode == PaintMode.Erase)
            {
                placementEngine.EraseStroke(center, ActiveProfile.brushSettings.radius, TargetData);
            }
        }
    }
}
