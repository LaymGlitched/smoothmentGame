using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StylizedGrassSystem
{
    [ExecuteAlways]
    public class GrassPainter : MonoBehaviour
    {
        [System.Serializable]
        public struct GrassPatch
        {
            public Vector3 positionWS;
            public Vector3 normalWS;
            public float radius;
            public float density;
        }

        [Header("References")]
        public ComputeShader grassCullingCompute;
        public Material grassMaterial;
        public Mesh grassBladeMesh;

        [Header("Generation Settings")]
        public float globalDensityMultiplier = 1.0f;
        public float heightOffset = 0f;
        public Vector2 sizeRange = new Vector2(0.8f, 1.2f);
        public Vector2 heightRange = new Vector2(0.8f, 1.5f);
        
        [Header("Colors")]
        public Color colorVariation1 = Color.white;
        public Color colorVariation2 = new Color(0.8f, 0.8f, 0.8f);

        [Header("Performance Settings")]
        public float cullDistance = 200f;
        [Range(0.1f, 0.9f), Tooltip("Distance ratio where LOD thinning begins. Lower = more aggressive.")]
        public float lodStartRatio = 0.3f;
        public UnityEngine.Rendering.ShadowCastingMode castShadows = UnityEngine.Rendering.ShadowCastingMode.Off;

        [HideInInspector]
        public List<GrassPatch> patches = new List<GrassPatch>();

        // Compute kernels
        private int generateKernel;
        private int cullKernel;

        // Persistent baked buffer (only rebuilt when patches change)
        private ComputeBuffer bakedInstanceBuffer;
        
        // Per-frame culling buffers
        private ComputeBuffer visibleBuffer;
        private ComputeBuffer argsBuffer;

        // Temporary generation buffers (released after bake)
        private ComputeBuffer patchBuffer;
        private ComputeBuffer prefixSumBuffer;

        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        private Vector4[] frustumPlanes = new Vector4[6];
        private bool initialized = false;
        private int totalBlades = 0;
        
        private bool needsRebuild = true;
        private float cachedDensityMultiplier = -1f;

        void OnEnable()
        {
            Initialize();
        }

        void OnDisable()
        {
            Cleanup();
        }

        public void Initialize()
        {
            if (grassCullingCompute == null || grassMaterial == null || grassBladeMesh == null)
                return;

            generateKernel = grassCullingCompute.FindKernel("GenerateGrass");
            cullKernel = grassCullingCompute.FindKernel("CullGrass");

            // Args buffer (persistent)
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            args[0] = (uint)grassBladeMesh.GetIndexCount(0);
            args[1] = 0;
            args[2] = (uint)grassBladeMesh.GetIndexStart(0);
            args[3] = (uint)grassBladeMesh.GetBaseVertex(0);
            args[4] = 0;
            argsBuffer.SetData(args);

            initialized = true;
            needsRebuild = true;
        }

        /// <summary>
        /// Runs the GenerateGrass kernel ONCE to bake all transforms into a persistent GPU buffer.
        /// </summary>
        private void BakeGrassInstances()
        {
            // Release old baked buffer
            if (bakedInstanceBuffer != null) { bakedInstanceBuffer.Release(); bakedInstanceBuffer = null; }
            if (visibleBuffer != null) { visibleBuffer.Release(); visibleBuffer = null; }
            if (patchBuffer != null) { patchBuffer.Release(); patchBuffer = null; }
            if (prefixSumBuffer != null) { prefixSumBuffer.Release(); prefixSumBuffer = null; }

            if (patches.Count == 0)
            {
                totalBlades = 0;
                args[1] = 0;
                argsBuffer.SetData(args);
                needsRebuild = false;
                return;
            }

            // Upload patch data
            patchBuffer = new ComputeBuffer(patches.Count, Marshal.SizeOf<GrassPatch>());
            patchBuffer.SetData(patches.ToArray());

            // Calculate prefix sums
            int[] prefixSums = new int[patches.Count];
            int currentSum = 0;
            for (int i = 0; i < patches.Count; i++)
            {
                currentSum += Mathf.CeilToInt(patches[i].density * globalDensityMultiplier);
                prefixSums[i] = currentSum;
            }
            totalBlades = currentSum;

            if (totalBlades == 0)
            {
                needsRebuild = false;
                return;
            }

            prefixSumBuffer = new ComputeBuffer(patches.Count, sizeof(int));
            prefixSumBuffer.SetData(prefixSums);

            // Create persistent baked buffer
            bakedInstanceBuffer = new ComputeBuffer(totalBlades, 80);

            // Create visible buffer (Append type, same max size)
            visibleBuffer = new ComputeBuffer(totalBlades, 80, ComputeBufferType.Append);

            // Set generation parameters & dispatch ONCE
            grassCullingCompute.SetInt("_PatchCount", patches.Count);
            grassCullingCompute.SetInt("_TotalBlades", totalBlades);
            grassCullingCompute.SetFloat("_GlobalDensityMultiplier", globalDensityMultiplier);
            grassCullingCompute.SetFloat("_HeightOffset", heightOffset);
            grassCullingCompute.SetVector("_SizeRange", sizeRange);
            grassCullingCompute.SetVector("_HeightRange", heightRange);
            grassCullingCompute.SetVector("_ColorVar1", (Vector4)colorVariation1);
            grassCullingCompute.SetVector("_ColorVar2", (Vector4)colorVariation2);

            grassCullingCompute.SetBuffer(generateKernel, "_Patches", patchBuffer);
            grassCullingCompute.SetBuffer(generateKernel, "_PrefixSums", prefixSumBuffer);
            grassCullingCompute.SetBuffer(generateKernel, "_OutputBuffer", bakedInstanceBuffer);

            int threadGroups = Mathf.CeilToInt(totalBlades / 64f);
            grassCullingCompute.Dispatch(generateKernel, threadGroups, 1, 1);

            // Release generation-only buffers (baked buffer stays alive)
            patchBuffer.Release(); patchBuffer = null;
            prefixSumBuffer.Release(); prefixSumBuffer = null;

            cachedDensityMultiplier = globalDensityMultiplier;
            needsRebuild = false;
        }

        void Cleanup()
        {
            initialized = false;
            if (bakedInstanceBuffer != null) { bakedInstanceBuffer.Release(); bakedInstanceBuffer = null; }
            if (visibleBuffer != null) { visibleBuffer.Release(); visibleBuffer = null; }
            if (argsBuffer != null) { argsBuffer.Release(); argsBuffer = null; }
            if (patchBuffer != null) { patchBuffer.Release(); patchBuffer = null; }
            if (prefixSumBuffer != null) { prefixSumBuffer.Release(); prefixSumBuffer = null; }
        }

        void LateUpdate()
        {
            if (!initialized) return;
            if (patches.Count == 0 && totalBlades == 0) return;

            // Check if density changed
            if (Mathf.Abs(cachedDensityMultiplier - globalDensityMultiplier) > 0.001f)
                needsRebuild = true;

            // Rebuild only when patches changed
            if (needsRebuild)
                BakeGrassInstances();

            if (totalBlades == 0 || bakedInstanceBuffer == null || visibleBuffer == null) return;

            Camera cam = Camera.main;
            #if UNITY_EDITOR
            if (Application.isEditor && !Application.isPlaying)
            {
                if (UnityEditor.SceneView.lastActiveSceneView != null)
                    cam = UnityEditor.SceneView.lastActiveSceneView.camera;
            }
            #endif
            if (cam == null) return;

            // ---- Per-frame: lightweight culling pass ----
            
            // Reset append counter
            visibleBuffer.SetCounterValue(0);

            // Set culling parameters
            grassCullingCompute.SetInt("_TotalBlades", totalBlades);
            grassCullingCompute.SetVector("_CameraPosition", cam.transform.position);
            grassCullingCompute.SetFloat("_CullDistance", cullDistance);
            grassCullingCompute.SetFloat("_LODStartRatio", lodStartRatio);

            // Frustum planes
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            for (int i = 0; i < 6; i++)
                frustumPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
            grassCullingCompute.SetVectorArray("_FrustumPlanes", frustumPlanes);

            // Bind buffers to cull kernel
            grassCullingCompute.SetBuffer(cullKernel, "_BakedInstances", bakedInstanceBuffer);
            grassCullingCompute.SetBuffer(cullKernel, "_VisibleBuffer", visibleBuffer);

            // Dispatch culling (256 threads per group for this lightweight kernel)
            int cullGroups = Mathf.CeilToInt(totalBlades / 256f);
            grassCullingCompute.Dispatch(cullKernel, cullGroups, 1, 1);

            // Copy visible count into indirect args (GPU → GPU, no CPU stall)
            ComputeBuffer.CopyCount(visibleBuffer, argsBuffer, sizeof(uint));

            // Bind visible buffer to material
            grassMaterial.SetBuffer("_VisibleInstances", visibleBuffer);

            // ONE draw call with only visible instances
            Bounds renderBounds = new Bounds(Vector3.zero, Vector3.one * 1000000f);
            Graphics.DrawMeshInstancedIndirect(grassBladeMesh, 0, grassMaterial, renderBounds, argsBuffer, 0, null, castShadows, true, gameObject.layer);
        }

        public void Rebuild()
        {
            Cleanup();
            Initialize();
        }

        public void AddPatch(Vector3 pos, Vector3 normal, float radius, float density)
        {
            patches.Add(new GrassPatch { positionWS = pos, normalWS = normal, radius = radius, density = density });
            needsRebuild = true;
        }

        public void ErasePatches(Vector3 pos, float radius)
        {
            bool removed = false;
            for (int i = patches.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(patches[i].positionWS, pos) < radius)
                {
                    patches.RemoveAt(i);
                    removed = true;
                }
            }
            if (removed)
                needsRebuild = true;
        }
        
        public void ClearAll()
        {
            patches.Clear();
            needsRebuild = true;
        }
    }
}
