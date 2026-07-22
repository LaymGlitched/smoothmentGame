#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Editor helper script that generates default SurfaceType ScriptableObject assets
    /// (Grass, Dirt, Stone, Concrete, Wood, Metal, Water, Sand, Snow) and a default MovementAudioProfile.
    /// Access via top menu: Tools -> FPMovement -> Create Default Audio Presets.
    /// </summary>
    public static class MovementAudioInitializer
    {
        private const string DefaultFolder = "Assets/Plugins/FPMovement/Audio/Presets";

        [MenuItem("Tools/FPMovement/Create Default Audio Presets")]
        public static void CreateDefaultAudioPresets()
        {
            if (!Directory.Exists(DefaultFolder))
            {
                Directory.CreateDirectory(DefaultFolder);
                AssetDatabase.Refresh();
            }

            string[] surfaceNames = new string[]
            {
                "Grass", "Dirt", "Stone", "Concrete", "Wood", "Metal", "Water", "Sand", "Snow"
            };

            MovementAudioProfile profile = AssetDatabase.LoadAssetAtPath<MovementAudioProfile>($"{DefaultFolder}/DefaultMovementAudioProfile.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MovementAudioProfile>();
                AssetDatabase.CreateAsset(profile, $"{DefaultFolder}/DefaultMovementAudioProfile.asset");
            }

            foreach (string sName in surfaceNames)
            {
                string path = $"{DefaultFolder}/{sName}.asset";
                SurfaceType surfaceAsset = AssetDatabase.LoadAssetAtPath<SurfaceType>(path);
                if (surfaceAsset == null)
                {
                    surfaceAsset = ScriptableObject.CreateInstance<SurfaceType>();
                    surfaceAsset.displayName = sName;
                    AssetDatabase.CreateAsset(surfaceAsset, path);
                }

                // Ensure profile has config entry for this surface
                bool exists = false;
                foreach (var cfg in profile.surfaceConfigs)
                {
                    if (cfg != null && cfg.surfaceType == surfaceAsset)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    SurfaceAudioConfig newCfg = new SurfaceAudioConfig
                    {
                        surfaceType = surfaceAsset,
                        volumeRange = new Vector2(0.85f, 1.0f),
                        pitchRange = new Vector2(0.95f, 1.05f),
                        stereoPanRange = 0.1f
                    };
                    profile.surfaceConfigs.Add(newCfg);
                }
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = profile;
            Debug.Log($"[FPMovement] Successfully created default surface assets and DefaultMovementAudioProfile at: {DefaultFolder}");
        }
    }
}
#endif
