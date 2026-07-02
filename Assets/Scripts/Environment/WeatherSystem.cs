using System.Collections.Generic;
using UnityEngine;

public class WeatherSystem : MonoBehaviour
{
    [Header("Current State")]
    [SerializeField]
    private WeatherState currentState;

    [SerializeField]
    private WeatherState targetState;

    [Header("Transition")]
    [SerializeField]
    private float transitionSpeed = 0.5f;
    private float transitionProgress = 1f;

    [Header("Weather List")]
    [SerializeField]
    private List<WeatherState> availableWeatherStates; // CHANGED NAME

    [Header("Weather Transitions")]
    [SerializeField]
    private float minWeatherDuration = 60f;

    [SerializeField]
    private float maxWeatherDuration = 300f;
    private float weatherChangeTimer = 0f;

    [Header("Forecast")]
    [SerializeField]
    private WeatherState[] forecast = new WeatherState[3];

    [SerializeField]
    private int forecastDay = 0;

    [Header("Events")]
    public System.Action<WeatherState> OnWeatherChanged;
    public System.Action<float> OnTransitionProgress;

    // Blended values
    public float CurrentCloudCoverage { get; private set; }
    public float CurrentRainAmount { get; private set; }
    public float CurrentWindStrength { get; private set; }
    public float CurrentFogDensity { get; private set; }
    public Color CurrentAmbientLight { get; private set; }
    public Color CurrentFogColor { get; private set; }
    public Color CurrentSunColor { get; private set; }
    public Color CurrentManaGlow { get; private set; }
    public float CurrentManaMultiplier { get; private set; }
    public float CurrentSpellCostMultiplier { get; private set; }

    public WeatherState CurrentState => currentState;
    public WeatherState TargetState => targetState;
    public bool IsTransitioning => transitionProgress < 1f;
    public WeatherState[] Forecast => forecast;
    public List<WeatherState> AvailableWeather => availableWeatherStates; // PROPERTY

    void Start()
    {
        if (currentState == null && availableWeatherStates.Count > 0)
        {
            SetWeatherInstant(availableWeatherStates[0]);
        }

        GenerateForecast();

        // Initialize timer
        weatherChangeTimer = Random.Range(minWeatherDuration, maxWeatherDuration);
    }

    void Update()
    {
        // Handle transition
        if (currentState == null || targetState == null)
            return;

        if (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime * transitionSpeed;
            transitionProgress = Mathf.Min(transitionProgress, 1f);

            UpdateBlendedValues();
            OnTransitionProgress?.Invoke(transitionProgress);

            if (transitionProgress >= 1f)
            {
                currentState = targetState;
                OnWeatherChanged?.Invoke(currentState);
            }
        }

        // --- Auto weather changes ---
        if (availableWeatherStates.Count > 1 && !IsTransitioning && currentState != null)
        {
            weatherChangeTimer -= Time.deltaTime;

            if (weatherChangeTimer <= 0f)
            {
                ChangeToRandomWeather();
                weatherChangeTimer = Random.Range(minWeatherDuration, maxWeatherDuration);
            }
        }
    }

    void UpdateBlendedValues()
    {
        float t = transitionProgress;
        float easeT = t * t * (3f - 2f * t);

        CurrentCloudCoverage = Mathf.Lerp(
            currentState.cloudCoverage,
            targetState.cloudCoverage,
            easeT
        );
        CurrentRainAmount = Mathf.Lerp(currentState.rainAmount, targetState.rainAmount, easeT);
        CurrentWindStrength = Mathf.Lerp(
            currentState.windStrength,
            targetState.windStrength,
            easeT
        );
        CurrentFogDensity = Mathf.Lerp(currentState.fogDensity, targetState.fogDensity, easeT);

        CurrentAmbientLight = Color.Lerp(
            currentState.ambientLight,
            targetState.ambientLight,
            easeT
        );
        CurrentFogColor = Color.Lerp(currentState.fogColor, targetState.fogColor, easeT);
        CurrentSunColor = Color.Lerp(currentState.sunColor, targetState.sunColor, easeT);
        CurrentManaGlow = Color.Lerp(currentState.manaGlowColor, targetState.manaGlowColor, easeT);

        CurrentManaMultiplier = Mathf.Lerp(
            currentState.manaMultiplier,
            targetState.manaMultiplier,
            easeT
        );
        CurrentSpellCostMultiplier = Mathf.Lerp(
            currentState.spellCostMultiplier,
            targetState.spellCostMultiplier,
            easeT
        );
    }

    public void SetWeather(WeatherState newWeather)
    {
        if (newWeather == null)
            return;

        targetState = newWeather;
        transitionProgress = 0f;
        OnWeatherChanged?.Invoke(targetState);
    }

    public void SetWeatherInstant(WeatherState newWeather)
    {
        if (newWeather == null)
            return;

        currentState = newWeather;
        targetState = newWeather;
        transitionProgress = 1f;

        CurrentCloudCoverage = newWeather.cloudCoverage;
        CurrentRainAmount = newWeather.rainAmount;
        CurrentWindStrength = newWeather.windStrength;
        CurrentFogDensity = newWeather.fogDensity;
        CurrentAmbientLight = newWeather.ambientLight;
        CurrentFogColor = newWeather.fogColor;
        CurrentSunColor = newWeather.sunColor;
        CurrentManaGlow = newWeather.manaGlowColor;
        CurrentManaMultiplier = newWeather.manaMultiplier;
        CurrentSpellCostMultiplier = newWeather.spellCostMultiplier;

        OnWeatherChanged?.Invoke(currentState);
    }

    void ChangeToRandomWeather()
    {
        if (availableWeatherStates.Count == 0)
            return;

        WeatherState newWeather = availableWeatherStates[
            Random.Range(0, availableWeatherStates.Count)
        ];

        int attempts = 0;
        while (newWeather == currentState && availableWeatherStates.Count > 1 && attempts < 10)
        {
            newWeather = availableWeatherStates[Random.Range(0, availableWeatherStates.Count)];
            attempts++;
        }

        SetWeather(newWeather);
        Debug.Log($"🌤 Weather changing to: {newWeather.displayName}");
    }

    public void ForceWeatherChange()
    {
        ChangeToRandomWeather();
        weatherChangeTimer = Random.Range(minWeatherDuration, maxWeatherDuration);
    }

    public void GenerateForecast()
    {
        if (availableWeatherStates.Count == 0)
            return;

        for (int i = 0; i < forecast.Length; i++)
        {
            forecast[i] = availableWeatherStates[Random.Range(0, availableWeatherStates.Count)];
        }
        forecastDay++;
    }

    public void AdvanceDay()
    {
        if (forecast.Length > 0)
        {
            SetWeather(forecast[0]);
            GenerateForecast();
        }
    }
}
