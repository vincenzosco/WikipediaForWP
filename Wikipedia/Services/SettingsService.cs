using System;
using Windows.Storage;

namespace Wikipedia.Services
{
    /// <summary>User preferences that survive restarts.</summary>
    public interface ISettingsService
    {
        /// <summary>Reading text scale. 0 small, 1 normal, 2 large.</summary>
        int FontScale { get; set; }

        /// <summary>Whether opened articles are recorded in the history.</summary>
        bool IsHistoryEnabled { get; set; }

        /// <summary>
        /// Wikipedia edition to read, as an edition code such as "ja" or
        /// "be-tarask". Empty means follow the device language.
        /// </summary>
        string WikiLanguage { get; set; }
    }

    /// <summary>
    /// Backed by ApplicationData.Current.LocalSettings, which is the WinRT
    /// equivalent of isolated storage settings. LocalSettings is plain text on
    /// disk, so nothing sensitive belongs here.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        private const string FontScaleKey = "FontScale";
        private const string HistoryEnabledKey = "HistoryEnabled";
        private const string WikiLanguageKey = "WikiLanguage";

        private const int DefaultFontScale = 1;

        public int FontScale
        {
            get { return ReadInt(FontScaleKey, DefaultFontScale); }
            set { Write(FontScaleKey, Clamp(value, 0, 2)); }
        }

        public bool IsHistoryEnabled
        {
            get { return ReadBool(HistoryEnabledKey, true); }
            set { Write(HistoryEnabledKey, value); }
        }

        public string WikiLanguage
        {
            get { return ReadString(WikiLanguageKey, string.Empty); }
            set { Write(WikiLanguageKey, value ?? string.Empty); }
        }

        private static int ReadInt(string key, int fallback)
        {
            try
            {
                var values = ApplicationData.Current.LocalSettings.Values;

                if (!values.ContainsKey(key))
                {
                    return fallback;
                }

                var raw = values[key];

                // Stored values come back as object, so the runtime type has
                // to be checked before the cast.
                if (raw is int)
                {
                    return (int)raw;
                }

                return fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static bool ReadBool(string key, bool fallback)
        {
            try
            {
                var values = ApplicationData.Current.LocalSettings.Values;

                if (!values.ContainsKey(key))
                {
                    return fallback;
                }

                var raw = values[key];

                if (raw is bool)
                {
                    return (bool)raw;
                }

                return fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static string ReadString(string key, string fallback)
        {
            try
            {
                var values = ApplicationData.Current.LocalSettings.Values;

                if (!values.ContainsKey(key))
                {
                    return fallback;
                }

                var raw = values[key] as string;

                return string.IsNullOrEmpty(raw) ? fallback : raw;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static void Write(string key, object value)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values[key] = value;
            }
            catch (Exception)
            {
                // A failed preference write should never break the UI.
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }
}
