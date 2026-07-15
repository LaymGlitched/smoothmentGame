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
            uiText.text = LocalizationBootstrapper.Instance.Get(translationKey);
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
