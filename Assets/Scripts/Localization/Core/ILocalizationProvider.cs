using System.Collections.Generic;
using System.Threading.Tasks;

namespace Reiteki.Localization.Core
{
    /// <summary>
    /// Defines a provider capable of loading localization data for a specific locale.
    /// </summary>
    public interface ILocalizationProvider
    {
        /// <summary>
        /// Asynchronously loads all localized entries for the given locale.
        /// </summary>
        /// <param name="locale">The locale code, e.g., "en-US".</param>
        /// <returns>A dictionary mapping translation keys to their localized entries, or null if the locale could not be loaded.</returns>
        Task<Dictionary<string, LocalizedEntry>> LoadLocaleAsync(string locale);
    }
}
