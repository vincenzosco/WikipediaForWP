using System;
using System.Collections.Generic;
using Windows.System.UserProfile;

namespace Wikipedia.Services
{
    /// <summary>
    /// Resolves which Wikipedia edition the app reads and searches.
    ///
    /// This is the content language, not the interface language. The interface
    /// text ships only in Italian and English and follows the device, while the
    /// edition can be any of the several hundred Wikipedia offers. Keeping the
    /// two apart is what removes the restart that switching used to need:
    /// nothing here touches resource qualification, so a new edition takes
    /// effect on the next request.
    /// </summary>
    public interface ILanguageService
    {
        /// <summary>Edition code being read, such as "ja" or "be-tarask".</summary>
        string WikiLanguage { get; }

        /// <summary>True when the current edition is written right to left.</summary>
        bool IsRightToLeft { get; }

        /// <summary>Base URL of the MediaWiki action API for this edition.</summary>
        string ApiBaseUrl { get; }

        /// <summary>Base URL of the REST search endpoint for this edition.</summary>
        string SearchBaseUrl { get; }

        /// <summary>Human readable link to an article on the web.</summary>
        string ArticleUrl(string title);

        /// <summary>True when the user picked an edition instead of following the device.</summary>
        bool HasChosenLanguage { get; }

        /// <summary>
        /// Switches the edition. An empty code goes back to following the
        /// device language.
        /// </summary>
        void SetLanguage(string code);
    }

    public sealed class LanguageService : ILanguageService
    {
        public const string Italian = "it";
        public const string English = "en";

        /// <summary>Edition used when the device language has no obvious match.</summary>
        public const string Fallback = English;

        /// <summary>Shortest edition code Wikipedia uses.</summary>
        private const int MinimumCodeLength = 2;

        /// <summary>
        /// Editions written right to left, taken from the dir property of the
        /// languageinfo meta. The catalog carries the same flag for every entry
        /// it returns, but the reading surfaces have to answer before the
        /// catalog has ever been loaded, so the set is repeated here as the
        /// answer that needs no network.
        /// </summary>
        private static readonly HashSet<string> RightToLeftCodes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "acm", "aeb", "aeb-arab", "apc", "ar", "arc", "arq", "ary", "arz",
                "azb", "bcc", "bgn", "bqi", "ckb", "dv", "fa", "glk", "he", "hno",
                "khw", "kk-arab", "kk-cn", "ks", "ks-arab", "ku-arab", "lki", "lrc",
                "luz", "ms-arab", "mzn", "nqo", "pnb", "ps", "sd", "sdh", "skr",
                "skr-arab", "ug", "ug-arab", "ur", "yi"
            };

        private readonly ISettingsService _settings;
        private readonly string _deviceLanguage;

        public LanguageService(ISettingsService settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            _settings = settings;
            _deviceLanguage = ResolveDeviceLanguage();
        }

        public string WikiLanguage
        {
            get
            {
                var chosen = _settings.WikiLanguage;

                return string.IsNullOrEmpty(chosen) ? _deviceLanguage : chosen;
            }
        }

        public bool IsRightToLeft
        {
            get { return IsRightToLeftCode(WikiLanguage); }
        }

        public bool HasChosenLanguage
        {
            get { return !string.IsNullOrEmpty(_settings.WikiLanguage); }
        }

        public void SetLanguage(string code)
        {
            _settings.WikiLanguage = string.IsNullOrEmpty(code) ? string.Empty : code.Trim();
        }

        public string ApiBaseUrl
        {
            get { return string.Format("https://{0}.wikipedia.org/w/api.php", WikiLanguage); }
        }

        public string SearchBaseUrl
        {
            get { return string.Format("https://{0}.wikipedia.org/w/rest.php/v1/search/page", WikiLanguage); }
        }

        public string ArticleUrl(string title)
        {
            var language = WikiLanguage;

            if (string.IsNullOrEmpty(title))
            {
                return string.Format("https://{0}.wikipedia.org", language);
            }

            return string.Format(
                "https://{0}.wikipedia.org/wiki/{1}",
                language,
                Uri.EscapeDataString(title.Replace(' ', '_')));
        }

        public static bool IsRightToLeftCode(string code)
        {
            return !string.IsNullOrEmpty(code) && RightToLeftCodes.Contains(code);
        }

        /// <summary>
        /// The first language on the user's own preference list, reduced to its
        /// primary subtag because that is the form Wikipedia uses as an edition
        /// code: fr-FR reads fr.wikipedia, and zh-Hans-CN reads zh.wikipedia.
        /// </summary>
        public static string ResolveDeviceLanguage()
        {
            try
            {
                foreach (var tag in GlobalizationPreferences.Languages)
                {
                    var code = PrimarySubtag(tag);

                    if (code != null)
                    {
                        return code;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to the documented fallback below.
            }

            return Fallback;
        }

        private static string PrimarySubtag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            var trimmed = tag.Trim();
            var separator = trimmed.IndexOf('-');

            if (separator > 0)
            {
                trimmed = trimmed.Substring(0, separator);
            }

            return trimmed.Length >= MinimumCodeLength ? trimmed.ToLowerInvariant() : null;
        }
    }
}
