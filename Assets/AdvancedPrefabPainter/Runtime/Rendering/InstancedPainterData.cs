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

        private bool isDirty = true;
        private int lastTotalCount = -1;

        private class RenderBatch
        {
            public Mesh mesh;
            public Material material;
            public Matrix4x4[][] matrixArrays;
        }

        private List<RenderBatch> cachedBatches = new List<RenderBatch>();

        public void AddInstance(GameObject prefab, Matrix4x4 matrix)
        {
            var data = paintedData.Find(d => d.prefab == prefab);
            if (data == null)
            {
                data = new PrefabInstanceData { prefab = prefab };
                paintedData.Add(data);
            }
            data.matrices.Add(matrix);
            isDirty = true;
        }

        public void RemoveInstanceAt(GameObject prefab, int index)
        {
            var data = paintedData.Find(d => d.prefab == prefab);
            if (data != null && index >= 0 && index < data.matrices.Count)
            {
                data.matrices.RemoveAt(index);
                isDirty = true;
            }
        }

        public void ClearAll()
        {
            paintedData.Clear();
            isDirty = true;
        }

        public void SetDirty()
        {
            isDirty = true;
        }

        private void OnEnable()
        {
            isDirty = true;
        }

        private void RebuildBatches()
        {
            cachedBatches.Clear();
            isDirty = false;

            if (paintedData == null || paintedData.Count == 0) return;

            foreach (var group in paintedData)
            {
                if (group.prefab == null || group.matrices.Count == 0) continue;

                var extracts = PrefabMeshCache.GetExtracts(group.prefab);
                if (extracts == null || extracts.Count == 0) continue;

                foreach (var extract in extracts)
                {
                    if (extract.mesh == null || extract.material == null) continue;

                    int total = group.matrices.Count;
                    int batchesCount = Mathf.CeilToInt(total / 1023f);

                    Matrix4x4[][] arrays = new Matrix4x4[batchesCount][];

                    for (int b = 0; b < batchesCount; b++)
                    {
                        int count = Mathf.Min(1023, total - b * 1023);
                        arrays[b] = new Matrix4x4[count];
                        for (int i = 0; i < count; i++)
                        {
                            arrays[b][i] = group.matrices[b * 1023 + i] * extract.localMatrix;
                        }
                    }

                    cachedBatches.Add(new RenderBatch
                    {
                        mesh = extract.mesh,
                        material = extract.material,
                        matrixArrays = arrays
                    });
                }
            }
        }

        private void Update()
        {
            int currentCount = 0;
            if (paintedData != null)
            {
                foreach (var g in paintedData) currentCount += g.matrices.Count;
            }

            if (currentCount != lastTotalCount)
            {
                isDirty = true;
                lastTotalCount = currentCount;
            }

            if (isDirty)
            {
                RebuildBatches();
            }

            if (cachedBatches.Count == 0) return;

            foreach (var batch in cachedBatches)
            {
                if (batch.mesh == null || batch.material == null || batch.matrixArrays == null) continue;

                for (int i = 0; i < batch.matrixArrays.Length; i++)
                {
                    Graphics.DrawMeshInstanced(
                        batch.mesh, 
                        0, 
                        batch.material, 
                        batch.matrixArrays[i],
                        batch.matrixArrays[i].Length,
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
