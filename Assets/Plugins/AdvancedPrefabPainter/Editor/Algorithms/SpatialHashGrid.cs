using System.Collections.Generic;
using UnityEngine;

namespace AdvancedPrefabPainter.Editor.Algorithms
{
    public struct SpatialPoint
    {
        public Vector3 position;
        public float radius;
    }

    public class SpatialHashGrid
    {
        private float cellSize;
        private Dictionary<Vector2Int, List<SpatialPoint>> grid;
        private float maxRadiusAdded = 0f;

        public SpatialHashGrid(float cellSize)
        {
            this.cellSize = cellSize > 0 ? cellSize : 1f;
            this.grid = new Dictionary<Vector2Int, List<SpatialPoint>>();
        }

        private Vector2Int GetCellCoords(Vector3 point)
        {
            int x = Mathf.FloorToInt(point.x / cellSize);
            int z = Mathf.FloorToInt(point.z / cellSize);
            return new Vector2Int(x, z);
        }

        public void Add(Vector3 point, float radius)
        {
            if (radius > maxRadiusAdded) maxRadiusAdded = radius;

            Vector2Int cell = GetCellCoords(point);
            if (!grid.TryGetValue(cell, out List<SpatialPoint> list))
            {
                list = new List<SpatialPoint>();
                grid[cell] = list;
            }
            list.Add(new SpatialPoint { position = point, radius = radius });
        }

        public void Remove(Vector3 point, float tolerance = 0.01f)
        {
            Vector2Int cell = GetCellCoords(point);
            if (grid.TryGetValue(cell, out List<SpatialPoint> list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (Vector3.Distance(list[i].position, point) < tolerance)
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public void Clear()
        {
            grid.Clear();
            maxRadiusAdded = 0f;
        }

        public bool HasOverlappingPoint(Vector3 center, float radius)
        {
            float searchRadius = radius + maxRadiusAdded;
            int minX = Mathf.FloorToInt((center.x - searchRadius) / cellSize);
            int maxX = Mathf.FloorToInt((center.x + searchRadius) / cellSize);
            int minZ = Mathf.FloorToInt((center.z - searchRadius) / cellSize);
            int maxZ = Mathf.FloorToInt((center.z + searchRadius) / cellSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (grid.TryGetValue(new Vector2Int(x, z), out List<SpatialPoint> cellList))
                    {
                        foreach (SpatialPoint p in cellList)
                        {
                            float distSq = new Vector2(p.position.x - center.x, p.position.z - center.z).sqrMagnitude;
                            float radSum = p.radius + radius;
                            if (distSq <= radSum * radSum)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
