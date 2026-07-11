using AdvancedPrefabPainter.Runtime.Data;
using AdvancedPrefabPainter.Runtime.Rendering;
using AdvancedPrefabPainter.Editor.Algorithms;
using UnityEngine;
using System.Collections.Generic;

namespace AdvancedPrefabPainter.Editor.Core
{
    public class PlacementEngine
    {
        private SpatialHashGrid spatialHash;

        public PlacementEngine()
        {
            spatialHash = new SpatialHashGrid(2f); // Cell size of 2m
        }

        public void ClearCache()
        {
            spatialHash.Clear();
        }

        public void RegisterExistingPoints(InstancedPainterData data, BiomeProfile profile)
        {
            ClearCache();
            if (data == null || data.paintedData == null) return;
            
            Dictionary<GameObject, float> radiusLookup = new Dictionary<GameObject, float>();
            if (profile != null && profile.prefabs != null)
            {
                foreach (var item in profile.prefabs.items)
                {
                    if (item.prefab != null && !radiusLookup.ContainsKey(item.prefab))
                    {
                        radiusLookup.Add(item.prefab, item.collisionRadius);
                    }
                }
            }

            foreach (var group in data.paintedData)
            {
                float rad = 0.5f; // fallback radius
                if (group.prefab != null && radiusLookup.TryGetValue(group.prefab, out float lookupRad))
                {
                    rad = lookupRad;
                }

                foreach (var matrix in group.matrices)
                {
                    spatialHash.Add(matrix.GetColumn(3), rad);
                }
            }
        }

        public void PaintStroke(Vector3 center, Vector3 normal, BiomeProfile profile, InstancedPainterData targetData)
        {
            if (profile == null || profile.brushSettings == null || profile.prefabs == null || targetData == null) return;

            var brush = profile.brushSettings;
            var candidates = OverlapSolver.GenerateCandidatePoints(
                new Vector2(center.x, center.z), 
                brush.radius, 
                brush.densityPerSqMeter, 
                brush.spacing
            );

            int prefabIndex = 0;

            foreach (var candidate2D in candidates)
            {
                // Noise masking
                if (profile.useNoiseMask)
                {
                    if (!NoiseGenerator.PassNoiseMask(candidate2D, profile.noiseScale, profile.noiseThreshold))
                        continue;
                }

                // Jitter
                Vector2 jittered = candidate2D;
                if (brush.jitter > 0)
                {
                    jittered += UnityEngine.Random.insideUnitCircle * brush.jitter * brush.spacing;
                }

                // Falloff check
                float distToCenter = Vector2.Distance(jittered, new Vector2(center.x, center.z));
                if (distToCenter > brush.radius) continue;

                float falloffProbability = GetFalloffProbability(distToCenter, brush.radius, brush);
                if (UnityEngine.Random.value > falloffProbability) continue;

                // Raycast downwards
                Vector3 rayOrigin = new Vector3(jittered.x, center.y + brush.radius, jittered.y);
                var sample = SurfaceSampler.SampleSurface(rayOrigin, Vector3.down, brush.radius * 2f, brush.hitMask);

                if (!sample.isValid) continue;

                if (!SurfaceSampler.CheckSlopeAndHeight(sample, brush.minSlope, brush.maxSlope, brush.useHeightFilter, brush.minHeight, brush.maxHeight))
                    continue;

                // Check overlap
                var prefabItem = profile.prefabs.GetNextItem(ref prefabIndex);
                if (prefabItem == null) continue;

                if (!OverlapSolver.CanPlace(spatialHash, sample.point, prefabItem.collisionRadius))
                    continue;

                // Success! Place object
                Vector3 scale = prefabItem.GetRandomScale();
                Quaternion rot = prefabItem.GetRandomRotation(sample.normal, brush.normalAlignment);
                Matrix4x4 mat = Matrix4x4.TRS(sample.point, rot, scale);
                
                targetData.AddInstance(prefabItem.prefab, mat);
                spatialHash.Add(sample.point, prefabItem.collisionRadius);
            }
        }

        public void EraseStroke(Vector3 center, float radius, InstancedPainterData targetData)
        {
            if (targetData == null || targetData.paintedData == null) return;

            float radiusSq = radius * radius;
            
            foreach (var group in targetData.paintedData)
            {
                for (int i = group.matrices.Count - 1; i >= 0; i--)
                {
                    Vector3 pos = group.matrices[i].GetColumn(3);
                    Vector2 pos2D = new Vector2(pos.x, pos.z);
                    Vector2 center2D = new Vector2(center.x, center.z);
                    
                    if ((pos2D - center2D).sqrMagnitude <= radiusSq)
                    {
                        spatialHash.Remove(pos);
                        group.matrices.RemoveAt(i);
                    }
                }
            }
        }

        private float GetFalloffProbability(float distance, float radius, BrushProfile brush)
        {
            float t = distance / radius;
            switch (brush.falloffType)
            {
                case BrushFalloffType.Constant:
                    return 1f;
                case BrushFalloffType.Linear:
                    return 1f - t;
                case BrushFalloffType.Smooth:
                    return Mathf.SmoothStep(1f, 0f, t);
                case BrushFalloffType.CustomCurve:
                    return brush.customFalloffCurve.Evaluate(t);
                default:
                    return 1f;
            }
        }
    }
}
