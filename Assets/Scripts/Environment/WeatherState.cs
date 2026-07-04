using UnityEngine;

namespace GameCode.Environment
{
    [CreateAssetMenu(fileName = "WeatherState", menuName = "Mana/Weather State")]
    public class WeatherState : ScriptableObject
    {
        [Header("Display")]
        public string displayName;
        public Sprite icon;

        [Header("Weather Properties")]
        [Range(0f, 1f)]
        public float cloudCoverage = 0f;

        [Range(0f, 1f)]
        public float rainAmount = 0f;

        [Range(0f, 1f)]
        public float windStrength = 0f;

        [Range(0f, 1f)]
        public float fogDensity = 0f;
        public Color fogColor = Color.gray;

        [Header("Visuals")]
        public Color ambientLight = Color.white;
        public Color sunColor = Color.white;
        public AnimationCurve sunIntensity = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public Gradient skyGradient;

        [Header("Effects")]
        public ParticleSystem rainPrefab;
        public ParticleSystem windParticles;
        public AudioClip ambientLoop;
        public float ambientVolume = 0.5f;

        [Header("Mana Properties")]
        [Range(0f, 2f)]
        public float manaMultiplier = 1f;

        [Range(0f, 2f)]
        public float spellCostMultiplier = 1f;

        [Range(0f, 2f)]
        public float spiritSpawnMultiplier = 1f;
        public bool unstableMagic = false;
        public Color manaGlowColor = Color.white;
    }
}
