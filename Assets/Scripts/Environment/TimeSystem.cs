using UnityEngine;

namespace GameCode.Environment
{
    public class TimeSystem : MonoBehaviour
    {
        [Header("Time Settings")]
        [SerializeField]
        private float timeOfDay = 6f; // 0-24, starting at 6am

        [SerializeField]
        private float dayLength = 1800f; // 30 minutes real-time

        [Header("Events")]
        public System.Action<float> OnTimeChanged;
        public System.Action<int> OnHourChanged;

        private int currentHour = -1;

        public float TimeOfDay
        {
            get => timeOfDay;
            private set => timeOfDay = value;
        }

        public float NormalizedTime => timeOfDay / 24f;
        public bool IsDaytime => timeOfDay >= 6 && timeOfDay < 18;
        public bool IsNight => !IsDaytime;
        public int CurrentHour => Mathf.FloorToInt(timeOfDay);
        public float MinutesUntilNextHour => (Mathf.CeilToInt(timeOfDay) - timeOfDay) * 60f;

        void Update()
        {
            timeOfDay += (24f / dayLength) * Time.deltaTime;

            if (timeOfDay >= 24f)
                timeOfDay -= 24f;

            OnTimeChanged?.Invoke(timeOfDay);

            // Hour changed event
            int newHour = CurrentHour;
            if (newHour != currentHour)
            {
                currentHour = newHour;
                OnHourChanged?.Invoke(newHour);
            }
        }

        public void SetTime(float hours)
        {
            timeOfDay = Mathf.Clamp(hours, 0f, 24f);
        }

        public void SetDayLength(float seconds)
        {
            dayLength = Mathf.Max(60f, seconds);
        }
    }
}
