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

        [Header("Ground Snapping")]
        [Tooltip("Raycast each blade down to the actual ground surface so grass wraps onto curved/uneven terrain.")]
        public bool snapToGround = true;
        [Tooltip("Maximum distance to search for ground below (and above) each blade.")]
        public float snapRayDistance = 20f;
        [Tooltip("Which layers count as ground for snapping.")]
        public LayerMask snapLayerMask = ~0;

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

        [StructLayout(LayoutKind.Sequential)]
        public struct GrassInstance
        {
            public Matrix4x4 mat;
            public Vector3 color;
            public float hash;
        }

        /// <summary>
        /// Generates all grass blade transforms on the CPU with per-blade raycasting,
        /// then uploads to GPU for culling. Each blade is individually snapped to the
        /// actual surface underneath it so grass wraps onto curved/uneven geometry.
        /// </summary>
        private void BakeGrassInstances()
        {
            // Release old buffers
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

            // Calculate blade counts per patch
            int[] bladeCounts = new int[patches.Count];
            int totalExpected = 0;
            for (int i = 0; i < patches.Count; i++)
            {
                bladeCounts[i] = Mathf.CeilToInt(patches[i].density * globalDensityMultiplier);
                totalExpected += bladeCounts[i];
            }

            if (totalExpected == 0)
            {
                totalBlades = 0;
                needsRebuild = false;
                return;
            }

            // Generate blade instances on CPU with per-blade ground raycasting
            List<GrassInstance> instanceData = new List<GrassInstance>(totalExpected);
            int actualBladeCount = 0;

            Vector3 colorVar1 = new Vector3(colorVariation1.r, colorVariation1.g, colorVariation1.b);
            Vector3 colorVar2 = new Vector3(colorVariation2.r, colorVariation2.g, colorVariation2.b);

            for (int patchIdx = 0; patchIdx < patches.Count; patchIdx++)
            {
                GrassPatch patch = patches[patchIdx];
                int bladeCount = bladeCounts[patchIdx];

                // Build tangent frame for scattering blades across the patch disc
                Vector3 normal = patch.normalWS.normalized;
                Vector3 tangent = Vector3.Cross(normal, Vector3.up);
                if (tangent.sqrMagnitude < 0.0001f)
                    tangent = Vector3.Cross(normal, Vector3.right);
                tangent.Normalize();
                Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

                for (int b = 0; b < bladeCount; b++)
                {
                    // Pseudo-random values matching the HLSL hash functions
                    float r1 = Hash12(new Vector2(patchIdx, b * 1.341f));
                    float r2 = Hash12(new Vector2(patchIdx, b * 2.713f));

                    // Scatter on the tangent-plane disc (same as GPU GetRandomPointInPatch)
                    float angle = r1 * Mathf.PI * 2f;
                    float dist = Mathf.Sqrt(r2) * patch.radius;
                    Vector3 flatPos = patch.positionWS
                        + tangent * Mathf.Cos(angle) * dist
                        + bitangent * Mathf.Sin(angle) * dist;

                    // Raycast to find the actual ground surface under this blade
                    Vector3 bladePos;
                    Vector3 bladeUp;

                    if (snapToGround)
                    {
                        Vector3 rayOrigin = flatPos + normal * 10f; // Start above along patch normal
                        if (Physics.Raycast(rayOrigin, -normal, out RaycastHit hit1, snapRayDistance * 2f, snapLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            bladePos = hit1.point;
                            bladeUp = hit1.normal.normalized;
                        }
                        // Fallback: straight-down raycast (for steep slopes where normal ray misses)
                        else if (Physics.Raycast(flatPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit2, snapRayDistance * 2f, snapLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            bladePos = hit2.point;
                            bladeUp = hit2.normal.normalized;
                        }
                        else
                        {
                            // No ground found — skip this blade entirely
                            continue;
                        }
                    }
                    else
                    {
                        bladePos = flatPos;
                        bladeUp = normal;
                    }

                    // Apply height offset along the surface normal
                    bladePos += bladeUp * heightOffset;

                    // Random per-blade properties
                    float widthHash = Hash12(new Vector2(bladePos.x * 1.1f, bladePos.z * 1.1f));
                    float heightHash = Hash12(new Vector2(bladePos.z * 1.7f, bladePos.x * 1.7f));
                    float yawHash = Hash12(new Vector2(bladePos.x * 2.3f, bladePos.y * 2.3f));

                    float width = Mathf.Lerp(sizeRange.x, sizeRange.y, widthHash);
                    float height = Mathf.Lerp(heightRange.x, heightRange.y, heightHash);
                    float yaw = yawHash * Mathf.PI * 2f;

                    // Build the transform matrix oriented to the surface normal
                    Vector3 forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                    forward = forward - bladeUp * Vector3.Dot(forward, bladeUp);
                    if (forward.sqrMagnitude < 0.0001f)
                        forward = Vector3.Cross(bladeUp, Vector3.right);
                    if (forward.sqrMagnitude < 0.0001f)
                        forward = Vector3.Cross(bladeUp, Vector3.forward);
                    forward.Normalize();
                    Vector3 right = Vector3.Cross(bladeUp, forward).normalized;

                    Matrix4x4 mat = new Matrix4x4();
                    mat.SetColumn(0, new Vector4(right.x * width, right.y * width, right.z * width, 0f));
                    mat.SetColumn(1, new Vector4(bladeUp.x * height, bladeUp.y * height, bladeUp.z * height, 0f));
                    mat.SetColumn(2, new Vector4(forward.x * width, forward.y * width, forward.z * width, 0f));
                    mat.SetColumn(3, new Vector4(bladePos.x, bladePos.y, bladePos.z, 1f));

                    // Color variation
                    float colorHash = Hash12(new Vector2(bladePos.y * 3.1f, bladePos.z * 3.1f));
                    Vector3 color = Vector3.Lerp(colorVar1, colorVar2, colorHash);

                    GrassInstance instance = new GrassInstance
                    {
                        mat = mat,
                        color = color,
                        hash = Hash13(bladePos)
                    };

                    instanceData.Add(instance);
                    actualBladeCount++;
                }
            }

            totalBlades = actualBladeCount;

            if (totalBlades == 0)
            {
                needsRebuild = false;
                return;
            }

            // Upload CPU-generated instances directly to GPU
            bakedInstanceBuffer = new ComputeBuffer(totalBlades, Marshal.SizeOf<GrassInstance>());
            bakedInstanceBuffer.SetData(instanceData.ToArray());

            visibleBuffer = new ComputeBuffer(totalBlades, Marshal.SizeOf<GrassInstance>(), ComputeBufferType.Append);

            cachedDensityMultiplier = globalDensityMultiplier;
            needsRebuild = false;
        }

        // ---- Hash functions matching the HLSL versions in GrassIncludes.hlsl ----

        private static float Frac(float x) => x - Mathf.Floor(x);

        private static float Hash12(Vector2 p)
        {
            Vector3 p3 = new Vector3(Frac(p.x * 0.1031f), Frac(p.y * 0.1031f), Frac(p.x * 0.1031f));
            float dot = p3.x * (p3.y + 33.33f) + p3.y * (p3.z + 33.33f) + p3.z * (p3.x + 33.33f);
            p3 = new Vector3(Frac(p3.x + dot), Frac(p3.y + dot), Frac(p3.z + dot));
            return Frac((p3.x + p3.y) * p3.z);
        }

        private static float Hash13(Vector3 p3)
        {
            p3 = new Vector3(Frac(p3.x * 0.1031f), Frac(p3.y * 0.1031f), Frac(p3.z * 0.1031f));
            float dot = p3.x * (p3.z + 31.32f) + p3.y * (p3.z + 31.32f) + p3.z * (p3.y + 31.32f);
            p3 = new Vector3(Frac(p3.x + dot), Frac(p3.y + dot), Frac(p3.z + dot));
            return Frac((p3.x + p3.y) * p3.z);
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
