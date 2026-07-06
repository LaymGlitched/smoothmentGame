using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(FloatingIslandGenerator))]
public class FloatingIslandGeneratorEditor : Editor
{
    private BoxBoundsHandle boundsHandle = new BoxBoundsHandle();
    private bool needsGeneration = false;

    private void OnSceneGUI()
    {
        FloatingIslandGenerator gen = (FloatingIslandGenerator)target;

        EditorGUI.BeginChangeCheck();

        Matrix4x4 handleMatrix = Matrix4x4.TRS(gen.transform.position, gen.transform.rotation, Vector3.one);

        boundsHandle.center = Vector3.zero;
        boundsHandle.size = gen.baseSize;

        Handles.color = Color.green;
        using (new Handles.DrawingScope(handleMatrix))
        {
            boundsHandle.DrawHandle();
        }

        float halfHeight = gen.baseSize.y * 0.5f;

        Vector3 equatorPos = gen.transform.TransformPoint(Vector3.up * (gen.equatorHeight * halfHeight));
        Handles.color = Color.cyan;
        float newEquator = Handles.ScaleValueHandle(
            gen.equatorHeight,
            equatorPos,
            gen.transform.rotation,
            HandleUtility.GetHandleSize(equatorPos) * 1.5f,
            Handles.SphereHandleCap,
            1f
        );

        Vector3 bottomPos = gen.transform.TransformPoint(Vector3.down * (gen.bottomDepth * halfHeight));
        Handles.color = Color.red;
        float newBottomDepth = Handles.ScaleValueHandle(
            gen.bottomDepth,
            bottomPos,
            gen.transform.rotation,
            HandleUtility.GetHandleSize(bottomPos) * 1.5f,
            Handles.ConeHandleCap,
            1f
        );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(gen, "Visual Edit Island");
            gen.baseSize = boundsHandle.size;
            gen.equatorHeight = Mathf.Clamp(newEquator, -0.6f, 0.4f);
            gen.bottomDepth = Mathf.Clamp(newBottomDepth, 0.5f, 4f);
            needsGeneration = true;
        }

        if (gen.useCage)
        {
            DrawAndEditCage(gen);
        }

        if (needsGeneration && GUIUtility.hotControl == 0)
        {
            gen.Generate();
            needsGeneration = false;
        }
    }

    // ---- Cage (rings of draggable points) ----

    private void DrawAndEditCage(FloatingIslandGenerator gen)
    {
        if (gen.cageRings == null || gen.cageRings.Count == 0) return;

        foreach (var ring in gen.cageRings)
            FloatingIslandGenerator.EnsureRingArrays(ring);

        var sorted = new List<FloatingIslandGenerator.CageRing>(gen.cageRings);
        sorted.Sort((a, b) => a.heightT.CompareTo(b.heightT));

        float halfHeight = gen.baseSize.y * 0.5f;
        float avgHorizRadius = 0.25f * (gen.baseSize.x + gen.baseSize.z);

        // Precompute every point's world position for this frame.
        var worldPositions = new List<Vector3[]>(sorted.Count);
        foreach (var ring in sorted)
        {
            int n = Mathf.Max(1, ring.pointCount);
            float sphereRadiusAtHeight = Mathf.Sqrt(Mathf.Max(0f, 1f - ring.heightT * ring.heightT));
            float baseRadius = avgHorizRadius * sphereRadiusAtHeight;

            Vector3[] positions = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float radius = n > 1 ? baseRadius * ring.radiusOffsets[i] : 0f;
                float angleRad = n > 1 ? (360f * i / n) * Mathf.Deg2Rad : 0f;
                float localY = ring.heightT * halfHeight + ring.heightOffsets[i] * halfHeight;
                Vector3 localPos = new Vector3(Mathf.Cos(angleRad) * radius, localY, Mathf.Sin(angleRad) * radius);
                positions[i] = gen.transform.TransformPoint(localPos);
            }
            worldPositions.Add(positions);
        }

        // Ring loop lines (around each ring).
        Handles.color = new Color(0.2f, 0.85f, 0.9f, 0.6f);
        for (int r = 0; r < sorted.Count; r++)
        {
            Vector3[] positions = worldPositions[r];
            if (positions.Length <= 1) continue;
            for (int i = 0; i < positions.Length; i++)
            {
                Handles.DrawLine(positions[i], positions[(i + 1) % positions.Length]);
            }
        }

        // Connecting lines between adjacent rings (nearest-angle match; naturally fans out to poles).
        Handles.color = new Color(0.95f, 0.75f, 0.2f, 0.55f);
        for (int r = 0; r < sorted.Count - 1; r++)
        {
            Vector3[] lower = worldPositions[r];
            Vector3[] upper = worldPositions[r + 1];
            FloatingIslandGenerator.CageRing lowerRing = sorted[r];
            FloatingIslandGenerator.CageRing upperRing = sorted[r + 1];

            for (int i = 0; i < lower.Length; i++)
            {
                int j = NearestAngleIndex(lowerRing, i, upperRing);
                Handles.DrawLine(lower[i], upper[j]);
            }
        }

        // Draggable point handles.
        for (int r = 0; r < sorted.Count; r++)
        {
            FloatingIslandGenerator.CageRing ring = sorted[r];
            Vector3[] positions = worldPositions[r];
            int n = positions.Length;
            float sphereRadiusAtHeight = Mathf.Sqrt(Mathf.Max(0f, 1f - ring.heightT * ring.heightT));
            float baseRadius = avgHorizRadius * sphereRadiusAtHeight;

            for (int i = 0; i < n; i++)
            {
                Vector3 worldPos = positions[i];
                float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.12f;
                Handles.color = n == 1 ? new Color(1f, 0.5f, 0.1f) : new Color(0.25f, 0.9f, 0.5f);

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPos = Handles.FreeMoveHandle(worldPos, handleSize, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(gen, "Sculpt Island Cage");
                    Vector3 newLocal = gen.transform.InverseTransformPoint(newWorldPos);

                    if (n > 1)
                    {
                        float newRadius = new Vector2(newLocal.x, newLocal.z).magnitude;
                        ring.radiusOffsets[i] = baseRadius > 0.001f ? Mathf.Clamp(newRadius / baseRadius, 0f, 3f) : 1f;
                    }

                    float newHeightWorld = newLocal.y - ring.heightT * halfHeight;
                    ring.heightOffsets[i] = halfHeight > 0.001f ? Mathf.Clamp(newHeightWorld / halfHeight, -1.5f, 1.5f) : 0f;

                    needsGeneration = true;
                }
            }
        }
    }

    private static int NearestAngleIndex(FloatingIslandGenerator.CageRing fromRing, int fromIndex, FloatingIslandGenerator.CageRing toRing)
    {
        int toCount = Mathf.Max(1, toRing.pointCount);
        if (toCount == 1) return 0;

        int fromCount = Mathf.Max(1, fromRing.pointCount);
        float fromAngle = fromCount > 1 ? 360f * fromIndex / fromCount : 0f;

        int best = 0;
        float bestDelta = float.MaxValue;
        for (int j = 0; j < toCount; j++)
        {
            float toAngle = 360f * j / toCount;
            float delta = Mathf.Abs(Mathf.DeltaAngle(fromAngle, toAngle));
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = j;
            }
        }
        return best;
    }

    // ---- Inspector ----

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

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Sculpt Cage", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag the green ring points in the Scene view outward/inward to bulge or pinch the mesh, or up/down to nudge its height. Orange points are the top/bottom poles.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Middle Ring"))
            {
                Undo.RecordObject(gen, "Add Cage Ring");
                var newRing = new FloatingIslandGenerator.CageRing { heightT = 0f, pointCount = 8 };
                FloatingIslandGenerator.EnsureRingArrays(newRing);

                if (gen.cageRings.Count >= 2)
                    gen.cageRings.Insert(gen.cageRings.Count - 1, newRing);
                else
                    gen.cageRings.Add(newRing);

                gen.Generate();
                EditorUtility.SetDirty(gen);
            }

            using (new EditorGUI.DisabledScope(gen.cageRings == null || gen.cageRings.Count <= 2))
            {
                if (GUILayout.Button("Remove Last Middle Ring"))
                {
                    Undo.RecordObject(gen, "Remove Cage Ring");
                    // Assumes the list is authored top-pole ... bottom-pole; remove the ring just before the last.
                    gen.cageRings.RemoveAt(gen.cageRings.Count - 2);
                    gen.Generate();
                    EditorUtility.SetDirty(gen);
                }
            }
        }

        if (GUILayout.Button("Reset Cage to Default"))
        {
            Undo.RecordObject(gen, "Reset Island Cage");
            gen.GenerateDefaultCage();
            gen.Generate();
            EditorUtility.SetDirty(gen);
        }
    }
}