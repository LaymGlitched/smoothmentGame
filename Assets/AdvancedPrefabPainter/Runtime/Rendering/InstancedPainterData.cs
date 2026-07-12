using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        [Header("Frustum Culling")]
        [SerializeField] private bool enableFrustumCulling = true;
        [Tooltip("Camera used for culling. Falls back to Camera.main, then the active Scene view camera in edit mode.")]
        [SerializeField] private Camera cullingCamera;
        [Tooltip("World-space size of the spatial grid cells instances are grouped into for culling.")]
        [SerializeField] private float cullCellSize = 10f;
        [Tooltip("How often (seconds) visibility is re-evaluated. Draw calls still happen every frame; only the frustum test is throttled.")]
        [SerializeField] private float cullUpdateInterval = 0.1f;
        [Tooltip("Extra world-space margin added around each instance's bounds to avoid pop-in at the frustum edge.")]
        [SerializeField] private float cullBoundsPadding = 0.5f;

        [Header("Distance Culling")]
        [SerializeField] private bool enableDistanceCulling = true;
        [Tooltip("Instances farther than this from the culling camera are not drawn.")]
        [SerializeField] private float maxRenderDistance = 50f;
        [Tooltip("Extra world-space margin added to the distance cutoff to avoid pop-out right at the edge.")]
        [SerializeField] private float distanceCullPadding = 2f;

        private bool isDirty = true;
        private int lastTotalCount = -1;
        private float nextCullUpdateTime;
        private bool loggedMissingMainCamera;

        private const int MaxInstancesPerDrawCall = 1023;

        // One entry per prefab group contributing to a batch, since different groups can
        // share the exact same mesh+material but need their own local pivot matrix and their
        // own per-group visibility list.
        private class DrawSource
        {
            public PrefabInstanceData group;
            public Matrix4x4 localMatrix;
        }

        // Keyed by (mesh, submeshIndex, material) so any painted prefabs that happen to share
        // all three get merged into the same draw call(s) instead of issuing a separate
        // DrawMeshInstanced per prefab. Submesh index has to be part of the key (not just
        // mesh+material): a multi-submesh mesh needs one draw call per submesh regardless,
        // and two submeshes that happen to share a material must stay separate batches or
        // DrawMeshInstanced ends up rendering the wrong submesh's geometry for one of them.
        // Unity's DrawMeshInstanced still hard-caps at 1023 instances per call (a GPU
        // constant-buffer limit), so a batch with more visible instances than that still
        // needs multiple calls - merging only removes the *avoidable* extra calls.
        private class RenderBatch
        {
            public Mesh mesh;
            public int submeshIndex;
            public Material material;
            public readonly List<DrawSource> sources = new List<DrawSource>();
        }

        // A spatial bucket of instance indices (into the owning group's matrices list) plus
        // a world-space AABB covering all of them, used as the frustum test target.
        private class Cell
        {
            public Bounds bounds;
            public bool boundsInitialized;
            public readonly List<int> indices = new List<int>();
        }

        private class GroupCullData
        {
            public float prefabRadius;
            public readonly Dictionary<long, Cell> cellLookup = new Dictionary<long, Cell>();
            public readonly List<Cell> cells = new List<Cell>();
        }

        private readonly List<RenderBatch> cachedBatches = new List<RenderBatch>();
        private readonly Dictionary<(Mesh mesh, int submeshIndex, Material material), RenderBatch> batchLookup = new Dictionary<(Mesh, int, Material), RenderBatch>();
        private readonly Dictionary<PrefabInstanceData, GroupCullData> cullDataByGroup = new Dictionary<PrefabInstanceData, GroupCullData>();
        private readonly Dictionary<PrefabInstanceData, List<int>> visibleIndicesByGroup = new Dictionary<PrefabInstanceData, List<int>>();
        private readonly List<PrefabInstanceData> staleGroupKeys = new List<PrefabInstanceData>();
        private readonly Plane[] frustumPlanes = new Plane[6];

        // Reused every draw call instead of allocating a new Matrix4x4[] per batch per frame.
        private readonly Matrix4x4[] scratchBuffer = new Matrix4x4[MaxInstancesPerDrawCall];

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

        private void OnValidate()
        {
            cullCellSize = Mathf.Max(0.5f, cullCellSize);
            cullUpdateInterval = Mathf.Max(0.02f, cullUpdateInterval);
            cullBoundsPadding = Mathf.Max(0f, cullBoundsPadding);
            maxRenderDistance = Mathf.Max(0f, maxRenderDistance);
            distanceCullPadding = Mathf.Max(0f, distanceCullPadding);
        }

        private void RebuildBatches()
        {
            cachedBatches.Clear();
            batchLookup.Clear();
            cullDataByGroup.Clear();
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

                    var key = (extract.mesh, extract.submeshIndex, extract.material);
                    if (!batchLookup.TryGetValue(key, out var batch))
                    {
                        batch = new RenderBatch { mesh = extract.mesh, submeshIndex = extract.submeshIndex, material = extract.material };
                        batchLookup[key] = batch;
                        cachedBatches.Add(batch);
                    }

                    batch.sources.Add(new DrawSource { group = group, localMatrix = extract.localMatrix });
                }

                BuildGroupCullData(group, extracts);
            }

            PruneStaleVisibilityEntries();
        }

        // visibleIndicesByGroup lists are now kept alive across cull updates for reuse (see
        // GetScratchVisibleList), so entries for groups that were removed or emptied need to
        // be dropped explicitly here instead of relying on a per-frame Clear().
        private void PruneStaleVisibilityEntries()
        {
            if (visibleIndicesByGroup.Count == 0) return;

            staleGroupKeys.Clear();
            foreach (var key in visibleIndicesByGroup.Keys)
            {
                if (!cullDataByGroup.ContainsKey(key))
                    staleGroupKeys.Add(key);
            }

            for (int i = 0; i < staleGroupKeys.Count; i++)
                visibleIndicesByGroup.Remove(staleGroupKeys[i]);
        }

        // Bins every instance of this prefab into a world-space grid so culling only has to
        // test a handful of cell bounds instead of every single instance.
        private void BuildGroupCullData(PrefabInstanceData group, List<PrefabExtract> extracts)
        {
            var cullData = new GroupCullData
            {
                prefabRadius = ComputePrefabRadius(extracts)
            };

            for (int i = 0; i < group.matrices.Count; i++)
            {
                Matrix4x4 m = group.matrices[i];
                Vector3 pos = new Vector3(m.m03, m.m13, m.m23);
                float radius = cullData.prefabRadius * GetMatrixMaxScale(m) + cullBoundsPadding;

                long key = CellKey(pos, cullCellSize);
                if (!cullData.cellLookup.TryGetValue(key, out var cell))
                {
                    cell = new Cell();
                    cullData.cellLookup[key] = cell;
                    cullData.cells.Add(cell);
                }

                Bounds pointBounds = new Bounds(pos, Vector3.one * (radius * 2f));
                if (!cell.boundsInitialized)
                {
                    cell.bounds = pointBounds;
                    cell.boundsInitialized = true;
                }
                else
                {
                    cell.bounds.Encapsulate(pointBounds);
                }

                cell.indices.Add(i);
            }

            cullDataByGroup[group] = cullData;
        }

        private static long CellKey(Vector3 pos, float cellSize)
        {
            int cx = Mathf.FloorToInt(pos.x / cellSize);
            int cz = Mathf.FloorToInt(pos.z / cellSize);
            return ((long)(uint)cx << 32) | (uint)cz;
        }

        // Conservative radius (from the prefab's local origin) that covers every extract's
        // mesh bounds, so a single instance's world-space footprint is prefabRadius * scale.
        private static float ComputePrefabRadius(List<PrefabExtract> extracts)
        {
            float maxDistSqr = 0f;
            for (int e = 0; e < extracts.Count; e++)
            {
                var extract = extracts[e];
                if (extract.mesh == null) continue;

                Bounds b = extract.mesh.bounds;
                Vector3 min = b.min;
                Vector3 max = b.max;

                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = new Vector3(
                        (c & 1) == 0 ? min.x : max.x,
                        (c & 2) == 0 ? min.y : max.y,
                        (c & 4) == 0 ? min.z : max.z);

                    Vector3 world = extract.localMatrix.MultiplyPoint3x4(corner);
                    float sqr = world.sqrMagnitude;
                    if (sqr > maxDistSqr) maxDistSqr = sqr;
                }
            }
            return Mathf.Sqrt(maxDistSqr);
        }

        private static float GetMatrixMaxScale(Matrix4x4 m)
        {
            float sx = new Vector3(m.m00, m.m10, m.m20).magnitude;
            float sy = new Vector3(m.m01, m.m11, m.m21).magnitude;
            float sz = new Vector3(m.m02, m.m12, m.m22).magnitude;
            return Mathf.Max(sx, Mathf.Max(sy, sz));
        }

        private Camera GetCullingCamera()
        {
            if (cullingCamera != null) return cullingCamera;
            if (Camera.main != null) return Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying && SceneView.lastActiveSceneView != null)
                return SceneView.lastActiveSceneView.camera;
#endif
            // Last resort: Camera.main only works if a camera is tagged "MainCamera".
            // Projects that forget that tag would otherwise silently get no culling at all.
            Camera fallback = FindFirstObjectByType<Camera>();
            if (fallback != null)
            {
                if (!loggedMissingMainCamera)
                {
                    Debug.LogWarning(
                        $"[InstancedPainterData] No camera is tagged 'MainCamera' and no 'Culling Camera' is assigned on '{name}'. " +
                        $"Falling back to '{fallback.name}' for culling. Assign the Culling Camera field or tag your camera " +
                        "MainCamera to make this explicit.", this);
                    loggedMissingMainCamera = true;
                }
                return fallback;
            }

            if (!loggedMissingMainCamera)
            {
                Debug.LogWarning(
                    $"[InstancedPainterData] No camera found for culling on '{name}' - frustum and distance culling are disabled " +
                    "until a camera exists (tag one MainCamera or assign Culling Camera).", this);
                loggedMissingMainCamera = true;
            }
            return null;
        }

        // Returns the reusable list for a group, clearing it in place. Keeping the List<int>
        // instances alive across cull updates means their backing array capacity is reused
        // instead of being reallocated (and GC'd) roughly ten times a second.
        private List<int> GetScratchVisibleList(PrefabInstanceData group)
        {
            if (!visibleIndicesByGroup.TryGetValue(group, out var list))
            {
                list = new List<int>();
                visibleIndicesByGroup[group] = list;
            }
            list.Clear();
            return list;
        }

        private void UpdateVisibility()
        {
            Camera cam = GetCullingCamera();
            bool doFrustumTest = enableFrustumCulling && cam != null;
            bool doDistanceTest = enableDistanceCulling && cam != null;

            if (!doFrustumTest && !doDistanceTest)
            {
                // No camera / both culling modes disabled -> treat everything as visible.
                foreach (var group in paintedData)
                {
                    if (group.matrices.Count == 0) continue;
                    var full = GetScratchVisibleList(group);
                    full.Capacity = Mathf.Max(full.Capacity, group.matrices.Count);
                    for (int i = 0; i < group.matrices.Count; i++) full.Add(i);
                }
                return;
            }

            if (doFrustumTest)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, frustumPlanes);
            }

            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float maxDistSqr = (maxRenderDistance + distanceCullPadding) * (maxRenderDistance + distanceCullPadding);

            foreach (var kvp in cullDataByGroup)
            {
                var group = kvp.Key;
                var cullData = kvp.Value;
                var visible = GetScratchVisibleList(group);

                for (int c = 0; c < cullData.cells.Count; c++)
                {
                    var cell = cullData.cells[c];

                    // Distance test first: cheap, and rejects far-away cells regardless of
                    // camera orientation before bothering with the frustum planes.
                    if (doDistanceTest)
                    {
                        float distSqr = cell.bounds.SqrDistance(camPos);
                        if (distSqr > maxDistSqr) continue;
                    }

                    if (doFrustumTest && !GeometryUtility.TestPlanesAABB(frustumPlanes, cell.bounds))
                        continue;

                    visible.AddRange(cell.indices);
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
                nextCullUpdateTime = 0f; // force a fresh visibility pass against the new data
            }

            if (cachedBatches.Count == 0) return;

            if (Time.time >= nextCullUpdateTime)
            {
                UpdateVisibility();
                nextCullUpdateTime = Time.time + cullUpdateInterval;
            }

            foreach (var batch in cachedBatches)
            {
                if (batch.mesh == null || batch.material == null) continue;
                DrawBatch(batch);
            }
        }

        private void DrawBatch(RenderBatch batch)
        {
            int bufferCount = 0;

            for (int s = 0; s < batch.sources.Count; s++)
            {
                var source = batch.sources[s];
                if (!visibleIndicesByGroup.TryGetValue(source.group, out var indices) || indices.Count == 0)
                    continue;

                var sourceMatrices = source.group.matrices;

                for (int i = 0; i < indices.Count; i++)
                {
                    scratchBuffer[bufferCount] = sourceMatrices[indices[i]] * source.localMatrix;
                    bufferCount++;

                    if (bufferCount == MaxInstancesPerDrawCall)
                    {
                        FlushDrawCall(batch, bufferCount);
                        bufferCount = 0;
                    }
                }
            }

            if (bufferCount > 0)
            {
                FlushDrawCall(batch, bufferCount);
            }
        }

        private void FlushDrawCall(RenderBatch batch, int count)
        {
            Graphics.DrawMeshInstanced(
                batch.mesh,
                batch.submeshIndex,
                batch.material,
                scratchBuffer,
                count,
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