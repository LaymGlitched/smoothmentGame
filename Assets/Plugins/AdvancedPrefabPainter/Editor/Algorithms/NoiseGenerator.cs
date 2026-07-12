using UnityEngine;

namespace AdvancedPrefabPainter.Editor.Algorithms
{
    public static class NoiseGenerator
    {
        public static float GetNoise(Vector2 position, float scale)
        {
            if (scale <= 0) return 1f;
            return Mathf.PerlinNoise(position.x / scale, position.y / scale);
        }

        public static bool PassNoiseMask(Vector2 position, float scale, float threshold)
        {
            return GetNoise(position, scale) >= threshold;
        }
    }
}
