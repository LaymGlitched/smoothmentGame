using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdvancedPrefabPainter.Runtime.Rendering
{
    [Serializable]
    public class PrefabInstanceData
    {
        public GameObject prefab;
        public List<Matrix4x4> matrices = new List<Matrix4x4>();
    }

    [ExecuteAlways]
    public class InstancedPainterData : MonoBehaviour
    {
        [SerializeField]
        public List<PrefabInstanceData> paintedData = new List<PrefabInstanceData>();

        public void AddInstance(GameObject prefab, Matrix4x4 matrix)
        {
            var data = paintedData.Find(d => d.prefab == prefab);
            if (data == null)
            {
                data = new PrefabInstanceData { prefab = prefab };
                paintedData.Add(data);
            }
            data.matrices.Add(matrix);
        }

        public void RemoveInstanceAt(GameObject prefab, int index)
        {
            var data = paintedData.Find(d => d.prefab == prefab);
            if (data != null && index >= 0 && index < data.matrices.Count)
            {
                data.matrices.RemoveAt(index);
            }
        }

        public void ClearAll()
        {
            paintedData.Clear();
        }

        private void Update()
        {
            if (paintedData == null || paintedData.Count == 0) return;

            // Optional: You could use Graphics.RenderMeshInstanced in Unity 2022+ for better performance
            // For simplicity and compatibility, we use Graphics.DrawMeshInstanced which handles max 1023 per call
            // We slice the matrices into 1023 blocks.
            
            foreach (var group in paintedData)
            {
                if (group.prefab == null || group.matrices.Count == 0) continue;

                var extracts = PrefabMeshCache.GetExtracts(group.prefab);
                if (extracts == null || extracts.Count == 0) continue;

                foreach (var extract in extracts)
                {
                    if (extract.mesh == null || extract.material == null) continue;

                    // Combine local matrix of the mesh with the instance world matrix
                    List<Matrix4x4> finalMatrices = new List<Matrix4x4>(group.matrices.Count);
                    for (int i = 0; i < group.matrices.Count; i++)
                    {
                        finalMatrices.Add(group.matrices[i] * extract.localMatrix);
                    }

                    int total = finalMatrices.Count;
                    int batches = Mathf.CeilToInt(total / 1023f);

                    for (int b = 0; b < batches; b++)
                    {
                        int count = Mathf.Min(1023, total - b * 1023);
                        var batchArray = finalMatrices.GetRange(b * 1023, count);
                        Graphics.DrawMeshInstanced(
                            extract.mesh, 
                            0, 
                            extract.material, 
                            batchArray,
                            null,
                            UnityEngine.Rendering.ShadowCastingMode.On,
                            true,
                            0,
                            null,
                            UnityEngine.Rendering.LightProbeUsage.BlendProbes
                        );
                    }
                }
            }
        }
    }
}
