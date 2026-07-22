using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// PhysicMaterial mapping pair for inspector configuration.
    /// </summary>
    [Serializable]
    public struct PhysicMaterialMapping
    {
        public PhysicMaterial physicMaterial;
        public SurfaceType surfaceType;
    }

    /// <summary>
    /// TerrainLayer mapping pair for inspector configuration.
    /// </summary>
    [Serializable]
    public struct TerrainLayerMapping
    {
        public TerrainLayer terrainLayer;
        public SurfaceType surfaceType;
    }

    /// <summary>
    /// Performs runtime ground surface detection beneath the player's feet.
    /// Supports custom <see cref="ISurfaceProvider"/> components, PhysicMaterials, and Terrain texture layers.
    /// Designed for O(1) cached lookups and zero GC allocations during gameplay.
    /// </summary>
    public class SurfaceDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("Layer mask for ground raycasting.")]
        [SerializeField]
        private LayerMask groundMask = ~0;

        [Tooltip("Distance below player's feet/origin to raycast.")]
        [SerializeField]
        private float raycastDistance = 1.2f;

        [Tooltip("Default surface type returned if no specific material match is found.")]
        [SerializeField]
        private SurfaceType defaultSurface;

        [Header("Material Mappings")]
        [Tooltip("Mapping of Physics/PhysicMaterials to SurfaceTypes.")]
        [SerializeField]
        private List<PhysicMaterialMapping> physicMaterialMappings = new List<PhysicMaterialMapping>();

        [Tooltip("Mapping of TerrainLayers to SurfaceTypes.")]
        [SerializeField]
        private List<TerrainLayerMapping> terrainLayerMappings = new List<TerrainLayerMapping>();

        public SurfaceType DefaultSurface => defaultSurface;

        // Cached runtime dictionaries for fast lookups
        private Dictionary<PhysicMaterial, SurfaceType> physicMatLookup = new Dictionary<PhysicMaterial, SurfaceType>();
        private Dictionary<TerrainLayer, SurfaceType> terrainLayerLookup = new Dictionary<TerrainLayer, SurfaceType>();

        // Cache components attached to colliders to prevent GC from GetComponent during gameplay
        private Dictionary<Collider, ISurfaceProvider> providerCache = new Dictionary<Collider, ISurfaceProvider>();
        private Dictionary<Collider, Terrain> terrainCache = new Dictionary<Collider, Terrain>();

        private RaycastHit cachedHit;
        private RigidbodyFPController controller;

        private void Awake()
        {
            controller = GetComponent<RigidbodyFPController>();
            BuildLookupTables();
        }

        /// <summary>
        /// Rebuilds runtime lookup dictionaries for PhysicMaterials and TerrainLayers.
        /// </summary>
        public void BuildLookupTables()
        {
            physicMatLookup.Clear();
            foreach (var mapping in physicMaterialMappings)
            {
                if (mapping.physicMaterial != null && mapping.surfaceType != null && !physicMatLookup.ContainsKey(mapping.physicMaterial))
                {
                    physicMatLookup.Add(mapping.physicMaterial, mapping.surfaceType);
                }
            }

            terrainLayerLookup.Clear();
            foreach (var mapping in terrainLayerMappings)
            {
                if (mapping.terrainLayer != null && mapping.surfaceType != null && !terrainLayerLookup.ContainsKey(mapping.terrainLayer))
                {
                    terrainLayerLookup.Add(mapping.terrainLayer, mapping.surfaceType);
                }
            }
        }

        /// <summary>
        /// Detects the <see cref="SurfaceType"/> currently beneath the player's feet.
        /// </summary>
        public SurfaceType DetectSurface()
        {
            Vector3 origin = transform.position;
            if (controller != null)
            {
                origin = controller.FeetPosition + Vector3.up * 0.1f;
            }

            if (Physics.Raycast(origin, Vector3.down, out cachedHit, raycastDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                Collider hitCollider = cachedHit.collider;
                if (hitCollider == null) return defaultSurface;

                // 1. Check for custom ISurfaceProvider / SurfaceTag
                if (!providerCache.TryGetValue(hitCollider, out ISurfaceProvider provider))
                {
                    provider = hitCollider.GetComponent<ISurfaceProvider>();
                    if (provider == null) provider = hitCollider.GetComponentInParent<ISurfaceProvider>();
                    providerCache[hitCollider] = provider; // Stores null if not present to avoid re-querying
                }

                if (provider != null)
                {
                    SurfaceType providerSurface = provider.GetSurfaceType();
                    if (providerSurface != null) return providerSurface;
                }

                // 2. Check for Terrain Texture Layer
                if (!terrainCache.TryGetValue(hitCollider, out Terrain terrain))
                {
                    terrain = hitCollider.GetComponent<Terrain>();
                    terrainCache[hitCollider] = terrain;
                }

                if (terrain != null && terrain.terrainData != null)
                {
                    SurfaceType terrainSurface = SampleTerrainSurface(terrain, cachedHit.point);
                    if (terrainSurface != null) return terrainSurface;
                }

                // 3. Check for PhysicMaterial / PhysicsMaterial
                PhysicMaterial mat = hitCollider.sharedMaterial;
                if (mat != null && physicMatLookup.TryGetValue(mat, out SurfaceType matchedSurface))
                {
                    return matchedSurface;
                }
            }

            return defaultSurface;
        }

        private SurfaceType SampleTerrainSurface(Terrain terrain, Vector3 worldPos)
        {
            TerrainData tData = terrain.terrainData;
            Vector3 tPos = terrain.transform.position;

            int mapX = Mathf.Clamp((int)(((worldPos.x - tPos.x) / tData.size.x) * tData.alphamapWidth), 0, tData.alphamapWidth - 1);
            int mapZ = Mathf.Clamp((int)(((worldPos.z - tPos.z) / tData.size.z) * tData.alphamapHeight), 0, tData.alphamapHeight - 1);

            float[,,] alphamap = tData.GetAlphamaps(mapX, mapZ, 1, 1);
            int numLayers = alphamap.GetLength(2);

            int dominantLayerIndex = 0;
            float maxWeight = 0f;

            for (int i = 0; i < numLayers; i++)
            {
                float weight = alphamap[0, 0, i];
                if (weight > maxWeight)
                {
                    maxWeight = weight;
                    dominantLayerIndex = i;
                }
            }

            TerrainLayer[] layers = tData.terrainLayers;
            if (dominantLayerIndex < layers.Length && layers[dominantLayerIndex] != null)
            {
                TerrainLayer dominantLayer = layers[dominantLayerIndex];
                if (terrainLayerLookup.TryGetValue(dominantLayer, out SurfaceType matchedSurface))
                {
                    return matchedSurface;
                }
            }

            return null;
        }

        /// <summary>
        /// Clears runtime component caches when scene or scene objects change.
        /// </summary>
        public void ClearCaches()
        {
            providerCache.Clear();
            terrainCache.Clear();
        }
    }
}
