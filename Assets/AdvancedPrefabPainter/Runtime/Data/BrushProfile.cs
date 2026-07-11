using UnityEngine;

namespace AdvancedPrefabPainter.Runtime.Data
{
    public enum BrushFalloffType
    {
        Constant,
        Linear,
        Smooth,
        CustomCurve
    }

    [CreateAssetMenu(fileName = "New Brush Profile", menuName = "Advanced Prefab Painter/Brush Profile")]
    public class BrushProfile : ScriptableObject
    {
        [Header("Brush Shape")]
        [Min(0.1f)]
        public float radius = 5f;
        
        public BrushFalloffType falloffType = BrushFalloffType.Smooth;
        public AnimationCurve customFalloffCurve = AnimationCurve.Linear(0, 1, 1, 0);

        [Header("Placement Density")]
        [Tooltip("Number of objects placed per square meter (approximate)")]
        [Min(0.01f)]
        public float densityPerSqMeter = 1f;

        [Min(0f)]
        public float spacing = 0.5f;

        [Range(0f, 1f)]
        public float jitter = 0.5f;

        [Header("Surface Alignment")]
        [Tooltip("How much the objects align to the surface normal (0 = up, 1 = surface normal)")]
        [Range(0f, 1f)]
        public float normalAlignment = 1f;
        
        [Header("Filtering")]
        public LayerMask hitMask = ~0;

        [Range(0f, 90f)]
        public float minSlope = 0f;
        [Range(0f, 90f)]
        public float maxSlope = 90f;

        public bool useHeightFilter = false;
        public float minHeight = -100f;
        public float maxHeight = 1000f;
    }
}
