using UnityEngine;

namespace AdvancedPrefabPainter.Runtime.Data
{
    [CreateAssetMenu(fileName = "New Biome Profile", menuName = "Advanced Prefab Painter/Biome Profile")]
    public class BiomeProfile : ScriptableObject
    {
        public string biomeName = "New Biome";
        public BrushProfile brushSettings;
        public PrefabPalette prefabs;

        [Header("Global Settings")]
        public bool useNoiseMask = false;
        public float noiseScale = 10f;
        public float noiseThreshold = 0.5f;
    }
}
