using System.Collections.Generic;
using UnityEngine;

namespace AdvancedPrefabPainter.Editor.Algorithms
{
    public static class OverlapSolver
    {
        public static List<Vector2> GenerateCandidatePoints(Vector2 center, float radius, float densityPerSqMeter, float spacing)
        {
            float area = Mathf.PI * radius * radius;
            int count = Mathf.CeilToInt(area * densityPerSqMeter);
            
            // Limit per stroke to avoid infinite loops if density is crazy high
            count = Mathf.Min(count, 5000); 

            List<Vector2> candidates = new List<Vector2>();

            for (int i = 0; i < count; i++)
            {
                Vector2 randomPoint = center + UnityEngine.Random.insideUnitCircle * radius;
                candidates.Add(randomPoint);
            }

            return candidates;
        }

        public static bool CanPlace(SpatialHashGrid grid, Vector3 position, float minimumDistance)
        {
            if (minimumDistance <= 0f) return true;
            return !grid.HasOverlappingPoint(position, minimumDistance);
        }
    }
}
