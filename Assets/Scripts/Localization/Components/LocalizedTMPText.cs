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
            if(uiText == null)
                uiText = GetComponent<TMP_Text>();

            LocalizationBootstrapper.Instance.LocaleChanged += RefreshText;
            RefreshText();
        }
        private void RefreshText()
        {
            if (LocalizationBootstrapper.Instance.TryGet(translationKey, out string translatedText))
            {
                uiText.text = translatedText;
            }
            else
            {
                // Optionally set it to empty or a loading state until the event fires
                uiText.text = "...";
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
