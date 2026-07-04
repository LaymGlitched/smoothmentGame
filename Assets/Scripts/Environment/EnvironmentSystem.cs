using UnityEngine;
using VLB;

namespace GameCode.Environment
{
    public class EnvironmentSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TimeSystem timeSystem;

        [SerializeField]
        private WeatherSystem weatherSystem;

        [SerializeField]
        private Light sunLight;

        [SerializeField]
        private Light moonLight;

        [SerializeField]
        private ReflectionProbe reflectionProbe;

        [SerializeField]
        private AudioSource ambientAudioSource;

        [SerializeField]
        private Transform playerTransform;

        [Header("Volumetric Fog Beam")]
        [SerializeField]
        private VolumetricLightBeamHD fogBeam;

        [SerializeField]
        private Vector3 fogBeamOffset = new Vector3(0, 20, 0);

        [Header("Sky Settings")]
        [SerializeField]
        private Material skyboxMaterial;

        [SerializeField]
        private AnimationCurve sunRotationCurve;

        [SerializeField]
        private float sunBaseAngle = 30f;

        [Header("Particle Systems")]
        [SerializeField]
        private ParticleSystem rainSystem;

        [SerializeField]
        private ParticleSystem windSystem;

        [SerializeField]
        private ParticleSystem manaGlowSystem;

        [Header("Particle Follow Settings")]
        [SerializeField]
        private Vector3 windLocalOffset = new Vector3(0, 0, 50);

        [Header("Sun Intensity Settings")]
        [SerializeField]
        private float sunBaseIntensity = 1f;

        [SerializeField]
        private float sunMaxIntensity = 2f;

        [SerializeField]
        private float sunMinIntensity = 0.05f;

        [Header("Night Settings")]
        [SerializeField]
        private float nightBrightness = 0.15f;

        [SerializeField]
        private float moonBrightness = 0.3f;

        [SerializeField]
        private Color nightAmbientColor = new Color(0.05f, 0.05f, 0.1f);

        [Header("Sun Temperature Settings")]
        [SerializeField]
        private float sunsetTemperature = 2420f;

        [SerializeField]
        private float middayTemperature = 4622f;

        [Header("Volumetric Fog Settings")]
        [SerializeField]
        private float fogSkyBlend = 0.3f;

        [SerializeField]
        private float fogMinIntensity = 0f;

        [SerializeField]
        private float fogMaxIntensity = 0.8f;

        [SerializeField]
        private float fogDensityThreshold = 0.3f;

        [SerializeField]
        private float noiseMinIntensity = 0f;

        [SerializeField]
        private float noiseMaxIntensity = 1f;

        [SerializeField]
        private float noiseMinScale = 0.5f;

        [SerializeField]
        private float noiseMaxScale = 2f;

        [SerializeField]
        private float fogTransitionSpeed = 2f;

        [SerializeField]
        private float jitteringMinFactor = 0f;

        [SerializeField]
        private float jitteringMaxFactor = 4f;

        [SerializeField]
        private float jitteringThreshold = 0.2f;

        [Header("Skybox Shader Properties")]
        [SerializeField]
        private string sunColorProperty = "_SunColor";

        [SerializeField]
        private string sunIntensityProperty = "_SunIntensity";

        [SerializeField]
        private string dayColorProperty = "_DayColor";

        [SerializeField]
        private string eveningColorProperty = "_EveningColor";

        [SerializeField]
        private string densityPowerProperty = "_DensityPower";

        [SerializeField]
        private string dayScatterProperty = "_DayScatterStrength";

        [SerializeField]
        private string eveningScatterProperty = "_EveningScatterStrength";

        [SerializeField]
        private string cloudColorProperty = "_CloudColor";

        [SerializeField]
        private string cloudBrightnessProperty = "_CloudBrightness";

        [SerializeField]
        private string cloudDensityProperty = "_CloudDensity";

        [SerializeField]
        private string cloudSpeedProperty = "_CloudSpeed";

        [SerializeField]
        private string starBrightnessProperty = "_StarBrightness";

        [Header("Mana Environment")]
        [SerializeField]
        private Color normalManaGlow = new Color(0.5f, 0.8f, 1f);

        [SerializeField]
        private Color intenseManaGlow = new Color(1f, 0.7f, 1f);

        [SerializeField]
        private Color weakManaGlow = new Color(0.3f, 0.4f, 0.5f);

        // Private variables
        private float currentSunIntensity = 1f;
        private float currentMoonIntensity = 1f;
        private float currentTemperature = 4622f;
        private float previousTimeOfDay = -1f;
        private float smoothSunHeight = 0f;
        private float smoothTemperature = 4622f;
        private float currentFogIntensity = 0f;

        // Store the ACTUAL colors being sent to the skybox
        private Color actualDayColor = Color.white;
        private Color actualEveningColor = new Color(1f, 0.6f, 0.3f);
        private Color actualHorizonColor = Color.gray;

        void Start()
        {
            if (timeSystem == null)
                timeSystem = FindAnyObjectByType<TimeSystem>();

            if (weatherSystem == null)
                weatherSystem = FindAnyObjectByType<WeatherSystem>();

            if (sunLight == null)
                sunLight = FindAnyObjectByType<Light>();

            if (skyboxMaterial == null)
                skyboxMaterial = RenderSettings.skybox;

            if (fogBeam == null)
                fogBeam = FindAnyObjectByType<VolumetricLightBeamHD>();

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }

            if (timeSystem != null)
            {
                timeSystem.OnTimeChanged += UpdateEnvironment;
                timeSystem.OnHourChanged += OnHourChanged;
            }

            if (weatherSystem != null)
            {
                weatherSystem.OnWeatherChanged += OnWeatherChanged;
                weatherSystem.OnTransitionProgress += UpdateEnvironment;
            }

            float initTime = timeSystem?.TimeOfDay ?? 6f;
            smoothSunHeight = sunRotationCurve.Evaluate(initTime / 24f);
            smoothTemperature = middayTemperature;

            RenderSettings.fog = false;

            UpdateEnvironment(initTime);
        }

        void LateUpdate()
        {
            FollowPlayerWithParticles();
            FollowPlayerWithFogBeam();
        }

        void FollowPlayerWithParticles()
        {
            if (playerTransform == null)
                return;

            if (rainSystem != null)
                rainSystem.transform.position = playerTransform.position;

            if (manaGlowSystem != null)
                manaGlowSystem.transform.position = playerTransform.position;

            if (windSystem != null)
            {
                Vector3 targetPos =
                    playerTransform.position + playerTransform.TransformDirection(windLocalOffset);
                windSystem.transform.position = targetPos;
            }
        }

        void FollowPlayerWithFogBeam()
        {
            if (fogBeam == null || playerTransform == null)
                return;

            Vector3 targetPos = playerTransform.position + fogBeamOffset;
            fogBeam.transform.position = Vector3.Lerp(
                fogBeam.transform.position,
                targetPos,
                Time.deltaTime * 5f
            );
        }

        void UpdateEnvironment(float timeOfDay)
        {
            if (previousTimeOfDay >= 23f && timeOfDay < 1f)
            {
                smoothSunHeight = 0f;
            }

            previousTimeOfDay = timeOfDay;

            UpdateSunAndMoon(timeOfDay);
            UpdateSkyboxAndFog(timeOfDay);
            UpdateWeatherEffects();
            UpdateManaEffects();
        }

        void UpdateSunAndMoon(float timeOfDay)
        {
            if (sunLight == null)
                return;

            float normalizedTime = timeOfDay / 24f;
            float targetSunHeight = sunRotationCurve.Evaluate(normalizedTime);

            if (Mathf.Abs(targetSunHeight - smoothSunHeight) > 0.5f)
            {
                smoothSunHeight = Mathf.Lerp(smoothSunHeight, targetSunHeight, Time.deltaTime * 2f);
            }
            else
            {
                smoothSunHeight = Mathf.Lerp(smoothSunHeight, targetSunHeight, Time.deltaTime * 5f);
            }

            float sunHeight = smoothSunHeight;
            float sunVerticalAngle = Mathf.Lerp(-90f, 90f, sunHeight);
            float sunHorizontalAngle = normalizedTime * 360f;

            Quaternion timeRotation = Quaternion.Euler(0f, sunHorizontalAngle, 0f);
            Quaternion heightRotation = Quaternion.Euler(sunVerticalAngle, 0f, 0f);
            sunLight.transform.rotation = timeRotation * heightRotation;

            float sunIntensity = Mathf.Lerp(nightBrightness, sunMaxIntensity, sunHeight);
            float noonBoost = Mathf.Sin(sunHeight * Mathf.PI) * 0.3f;
            sunIntensity += noonBoost;
            sunIntensity = Mathf.Clamp(sunIntensity, nightBrightness, sunMaxIntensity * 1.3f);

            float cloudCover = weatherSystem?.CurrentCloudCoverage ?? 0f;
            float weatherMod = 1f - cloudCover * 0.7f;
            currentSunIntensity = sunIntensity * weatherMod;
            sunLight.intensity = currentSunIntensity;

            Color ambientColor = Color.Lerp(nightAmbientColor, Color.white * 0.8f, sunHeight);
            RenderSettings.ambientLight = ambientColor;

            if (weatherSystem != null && weatherSystem.CurrentState != null)
            {
                Color sunColor = weatherSystem.CurrentSunColor;
                float warmth = 1f - Mathf.Abs(sunHeight - 0.5f) * 2f;
                Color warmColor = Color.Lerp(sunColor, new Color(1f, 0.6f, 0.3f), warmth * 0.5f);

                if (warmColor.r < 0.01f && warmColor.g < 0.01f && warmColor.b < 0.01f)
                {
                    warmColor = Color.white;
                }

                sunLight.color = warmColor;

                float targetTemp = Mathf.Lerp(
                    sunsetTemperature,
                    middayTemperature,
                    Mathf.Clamp01(sunHeight * 1.5f)
                );
                targetTemp = Mathf.Lerp(targetTemp, targetTemp * 0.8f, cloudCover * 0.5f);
                smoothTemperature = Mathf.Lerp(smoothTemperature, targetTemp, Time.deltaTime * 3f);
                currentTemperature = smoothTemperature;

                sunLight.useColorTemperature = true;

                if (skyboxMaterial != null && warmColor.r > 0.01f)
                {
                    skyboxMaterial.SetColor(sunColorProperty, warmColor);
                    skyboxMaterial.SetFloat(sunIntensityProperty, currentSunIntensity);
                }
            }
            else
            {
                sunLight.color = Color.white;
                sunLight.useColorTemperature = false;
            }

            if (moonLight != null)
            {
                float moonIntensity = Mathf.Lerp(moonBrightness, 0.05f, sunHeight);
                currentMoonIntensity = moonIntensity;
                moonLight.intensity = currentMoonIntensity;

                Quaternion moonTimeRotation = Quaternion.Euler(0f, sunHorizontalAngle + 180f, 0f);
                Quaternion moonHeightRotation = Quaternion.Euler(-sunVerticalAngle, 0f, 0f);
                moonLight.transform.rotation = moonTimeRotation * moonHeightRotation;
            }
        }

        void UpdateSkyboxAndFog(float timeOfDay)
        {
            if (skyboxMaterial == null)
                return;

            float sunHeight = smoothSunHeight;
            float dayFactor = Mathf.Clamp01(sunHeight * 1.5f);

            Color weatherColor = weatherSystem?.CurrentFogColor ?? Color.gray;
            float cloudCover = weatherSystem?.CurrentCloudCoverage ?? 0f;

            Color dayColor = Color.Lerp(Color.white, weatherColor, cloudCover * 0.3f);
            Color eveningColor = Color.Lerp(
                new Color(1f, 0.6f, 0.3f),
                weatherColor,
                cloudCover * 0.5f
            );

            actualDayColor = dayColor;
            actualEveningColor = eveningColor;

            Color horizonColor = Color.Lerp(eveningColor, dayColor, dayFactor);
            horizonColor = Color.Lerp(horizonColor, weatherColor, cloudCover * 0.3f);
            actualHorizonColor = horizonColor;

            skyboxMaterial.SetColor(dayColorProperty, dayColor);
            skyboxMaterial.SetColor(eveningColorProperty, eveningColor);
            skyboxMaterial.SetFloat("_EveningThreshold", 1.5f);
            skyboxMaterial.SetFloat(densityPowerProperty, 10f);
            skyboxMaterial.SetFloat(dayScatterProperty, 1f);
            skyboxMaterial.SetFloat(eveningScatterProperty, 12f);

            Color cloudColor = Color.Lerp(Color.white, weatherColor, cloudCover * 0.5f);
            skyboxMaterial.SetColor(cloudColorProperty, cloudColor);
            skyboxMaterial.SetFloat(cloudBrightnessProperty, 0.9f);
            skyboxMaterial.SetFloat(cloudDensityProperty, Mathf.Lerp(0.5f, 3f, cloudCover));
            skyboxMaterial.SetFloat(cloudSpeedProperty, 5f);

            float starBrightness = Mathf.Lerp(0.99f, 0f, dayFactor);
            skyboxMaterial.SetFloat(starBrightnessProperty, starBrightness);
            skyboxMaterial.SetFloat("_SunSize", 0.082f);

            UpdateVolumetricFog(sunHeight, dayFactor);
        }

        void UpdateVolumetricFog(float sunHeight, float dayFactor)
        {
            if (fogBeam == null || weatherSystem == null)
                return;

            float cloudCover = weatherSystem.CurrentCloudCoverage;
            float windStrength = weatherSystem.CurrentWindStrength;

            // --- Calculate fog color from skybox ---
            Color skyHorizonColor = Color.Lerp(actualEveningColor, actualDayColor, dayFactor);
            Color weatherFogColor = weatherSystem.CurrentFogColor;

            Color targetColor = Color.Lerp(skyHorizonColor, weatherFogColor, fogSkyBlend);
            float fogStrength = Mathf.Clamp01(weatherSystem.CurrentFogDensity * 2f);
            targetColor = Color.Lerp(skyHorizonColor, targetColor, fogStrength);

            // --- Set beam color ---
            if (fogBeam.colorMode == ColorMode.Flat)
            {
                fogBeam.colorFlat = targetColor;
            }
            else if (fogBeam.colorMode == ColorMode.Gradient)
            {
                fogBeam.colorMode = ColorMode.Flat;
                fogBeam.colorFlat = targetColor;
            }

            // --- Set intensity ---
            float targetIntensity = Mathf.Lerp(fogMinIntensity, fogMaxIntensity, cloudCover);
            currentFogIntensity = Mathf.Lerp(
                currentFogIntensity,
                targetIntensity,
                Time.deltaTime * fogTransitionSpeed
            );
            fogBeam.intensity = currentFogIntensity;

            // --- 3D Noise Control ---
            bool isFogDense = cloudCover > fogDensityThreshold;

            if (isFogDense && fogBeam.noiseMode == NoiseMode.Disabled)
            {
                fogBeam.noiseMode = NoiseMode.WorldSpace;
            }
            else if (!isFogDense && fogBeam.noiseMode != NoiseMode.Disabled)
            {
                fogBeam.noiseMode = NoiseMode.Disabled;
            }

            float noiseIntensity = Mathf.Clamp01(
                (cloudCover - fogDensityThreshold) / (1f - fogDensityThreshold)
            );
            fogBeam.noiseIntensity = Mathf.Lerp(
                noiseMinIntensity,
                noiseMaxIntensity,
                noiseIntensity
            );

            float noiseScale = Mathf.Lerp(noiseMinScale, noiseMaxScale, windStrength);
            fogBeam.noiseScaleLocal = noiseScale;

            fogBeam.noiseVelocityLocal = new Vector3(
                windStrength * 0.5f,
                0.2f,
                windStrength * 0.3f
            );

            // --- JITTERING CONTROL ---
            // Jittering helps reduce banding artifacts when using shadows/cookies with low raymarching steps
            float jitteringInput = Mathf.Max(cloudCover, windStrength * 0.5f);
            float jitteringFactor = 0f;

            if (jitteringInput > jitteringThreshold)
            {
                float normalizedJitter = Mathf.Clamp01(
                    (jitteringInput - jitteringThreshold) / (1f - jitteringThreshold)
                );
                jitteringFactor = Mathf.Lerp(
                    jitteringMinFactor,
                    jitteringMaxFactor,
                    normalizedJitter
                );

                // Add slight wind influence for dynamic jittering
                jitteringFactor += windStrength * 0.5f;
                jitteringFactor = Mathf.Clamp(
                    jitteringFactor,
                    jitteringMinFactor,
                    jitteringMaxFactor
                );
            }

            fogBeam.jitteringFactor = jitteringFactor;

            // --- Update beam geometry ---
            float rangeMultiplier = Mathf.Lerp(0.5f, 1.5f, cloudCover);
            fogBeam.fallOffEnd = 100f * rangeMultiplier;

            fogBeam.UpdateAfterManualPropertyChange();
        }

        void UpdateWeatherEffects()
        {
            if (weatherSystem == null)
                return;

            if (rainSystem != null)
            {
                float rainAmount = weatherSystem.CurrentRainAmount;

                if (rainAmount > 0.01f && !rainSystem.isPlaying)
                    rainSystem.Play();
                else if (rainAmount <= 0.01f && rainSystem.isPlaying)
                    rainSystem.Stop();

                var emission = rainSystem.emission;
                emission.rateOverTime = rainAmount * 1000f;
            }

            if (windSystem != null)
            {
                float wind = weatherSystem.CurrentWindStrength;

                if (wind > 0.01f && !windSystem.isPlaying)
                    windSystem.Play();
                else if (wind <= 0.01f && windSystem.isPlaying)
                    windSystem.Stop();

                var velocity = windSystem.velocityOverLifetime;
                velocity.x = wind * 5f;
            }

            if (ambientAudioSource != null && weatherSystem.CurrentState != null)
            {
                var weatherState = weatherSystem.CurrentState;
                if (weatherState.ambientLoop != ambientAudioSource.clip)
                {
                    ambientAudioSource.clip = weatherState.ambientLoop;
                    ambientAudioSource.volume = weatherState.ambientVolume;
                    ambientAudioSource.Play();
                }
            }
        }

        void UpdateManaEffects()
        {
            if (manaGlowSystem == null)
                return;

            float manaMultiplier = weatherSystem?.CurrentManaMultiplier ?? 1f;

            if (manaMultiplier > 1.1f || manaMultiplier < 0.9f)
            {
                if (!manaGlowSystem.isPlaying)
                    manaGlowSystem.Play();

                var main = manaGlowSystem.main;

                if (manaMultiplier > 1.1f)
                {
                    main.startColor = Color.Lerp(
                        normalManaGlow,
                        intenseManaGlow,
                        (manaMultiplier - 1f) / 1f
                    );
                    main.startLifetime = 2f * manaMultiplier;
                }
                else
                {
                    main.startColor = Color.Lerp(weakManaGlow, normalManaGlow, manaMultiplier);
                    main.startLifetime = 2f * manaMultiplier;
                }

                var emission = manaGlowSystem.emission;
                emission.rateOverTime = 50f * Mathf.Abs(manaMultiplier - 1f) * 2f;
            }
            else
            {
                if (manaGlowSystem.isPlaying)
                    manaGlowSystem.Stop();
            }

            if (reflectionProbe != null)
            {
                reflectionProbe.intensity = 1f + (manaMultiplier - 1f) * 0.5f;
            }
        }

        void OnWeatherChanged(WeatherState newWeather)
        {
            UpdateEnvironment(timeSystem?.TimeOfDay ?? 6f);
        }

        void OnHourChanged(int hour)
        {
            if (hour == 0)
            {
                smoothSunHeight = 0f;
            }
        }

        [ContextMenu("Generate Default Sun Curve")]
        void GenerateDefaultSunCurve()
        {
            if (sunRotationCurve == null)
                sunRotationCurve = new AnimationCurve();

            sunRotationCurve.keys = new Keyframe[]
            {
                new Keyframe(0f, 0f),
                new Keyframe(0.21f, 0.1f),
                new Keyframe(0.25f, 0.3f),
                new Keyframe(0.33f, 0.7f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.67f, 0.7f),
                new Keyframe(0.75f, 0.3f),
                new Keyframe(0.79f, 0.1f),
                new Keyframe(1f, 0f),
            };

            for (int i = 0; i < sunRotationCurve.keys.Length; i++)
            {
                sunRotationCurve.SmoothTangents(i, 0f);
            }

            Debug.Log("Sun curve generated!");
        }

        void OnDestroy()
        {
            if (timeSystem != null)
            {
                timeSystem.OnTimeChanged -= UpdateEnvironment;
                timeSystem.OnHourChanged -= OnHourChanged;
            }

            if (weatherSystem != null)
            {
                weatherSystem.OnWeatherChanged -= OnWeatherChanged;
                weatherSystem.OnTransitionProgress -= UpdateEnvironment;
            }
        }
    }
}
