using TMPro;
using UnityEngine;

namespace Reiteki.Localization
{
    public class LocalizedTMPText : MonoBehaviour
    {
        public string translationKey;
        public TMP_Text uiText;
        private void Start()
        {
            if (uiText == null)
                uiText = GetComponent<TMP_Text>();

            if (LocalizationBootstrapper.Instance != null)
            {
                LocalizationBootstrapper.Instance.LocaleChanged += RefreshText;
            }

            RefreshText();
        }

        private void RefreshText()
        {
            if (uiText == null)
                return;

            if (LocalizationBootstrapper.Instance != null && LocalizationBootstrapper.Instance.TryGet(translationKey, out string translatedText))
            {
                uiText.text = translatedText;
            }
            else if (!string.IsNullOrEmpty(translationKey))
            {
                uiText.text = LocalizationBootstrapper.Instance != null ? LocalizationBootstrapper.Instance.Get(translationKey) : translationKey;
            }
        }

        private void OnDestroy()
        {
            if (LocalizationBootstrapper.Instance != null)
            {
                LocalizationBootstrapper.Instance.LocaleChanged -= RefreshText;
            }
        }
    }
}
