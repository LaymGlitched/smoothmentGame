#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Drawers
{
    /// <summary>
    /// High-performance cache for ScriptableObject assets used by CustomPropertyDrawers.
    /// Eliminates expensive AssetDatabase.FindAssets and LoadAssetAtPath disk lookups
    /// during IMGUI OnGUI calls.
    /// </summary>
    public static class EditorAssetCache<T> where T : ScriptableObject
    {
        private static List<T> _cachedAssets;
        private static double _lastFetchTime;
        private const double CacheDurationSeconds = 2.0; // Refresh at most every 2 seconds

        public static List<T> GetAssets()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (_cachedAssets == null || currentTime - _lastFetchTime > CacheDurationSeconds)
            {
                _lastFetchTime = currentTime;
                string filter = $"t:{typeof(T).Name}";
                string[] guids = AssetDatabase.FindAssets(filter);
                _cachedAssets = guids
                    .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(a => a != null)
                    .ToList();
            }
            return _cachedAssets;
        }

        public static void ClearCache()
        {
            _cachedAssets = null;
            _lastFetchTime = 0;
        }
    }
}
#endif
