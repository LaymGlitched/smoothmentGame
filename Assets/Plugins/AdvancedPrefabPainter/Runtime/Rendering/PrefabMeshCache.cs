using System.Collections.Generic;
using UnityEngine;

namespace AdvancedPrefabPainter.Runtime.Rendering
{
    public class PrefabExtract
    {
        public Mesh mesh;
        public int submeshIndex;
        public Material material;
        public Matrix4x4 localMatrix;
    }

    public static class PrefabMeshCache
    {
        private static Dictionary<GameObject, List<PrefabExtract>> cache = new Dictionary<GameObject, List<PrefabExtract>>();

        public static List<PrefabExtract> GetExtracts(GameObject prefab)
        {
            if (prefab == null) return null;
            if (cache.TryGetValue(prefab, out var extracts))
            {
                return extracts;
            }

            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            extracts = new List<PrefabExtract>(renderers.Length);
            foreach (var renderer in renderers)
            {
                if (renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                {
                    Matrix4x4 localMat = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;

                    for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                    {
                        Material mat = null;
                        if (renderer.sharedMaterials.Length > i)
                        {
                            mat = renderer.sharedMaterials[i];
                        }
                        else if (renderer.sharedMaterials.Length > 0)
                        {
                            mat = renderer.sharedMaterials[0];
                        }

                        extracts.Add(new PrefabExtract
                        {
                            mesh = filter.sharedMesh,
                            submeshIndex = i,
                            material = mat,
                            localMatrix = localMat
                        });
                    }
                }
            }

            cache[prefab] = extracts;
            return extracts;
        }

        public static void ClearCache()
        {
            cache.Clear();
        }
    }
}