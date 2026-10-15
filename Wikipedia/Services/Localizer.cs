using System;
using System.Globalization;
using Windows.ApplicationModel.Resources;

namespace Wikipedia.Services
{
    /// <summary>
    /// Thin wrapper over ResourceLoader. The Windows resource qualifier system
    /// already picks the best match for the device language, so this type only
    /// adds a null safe fallback: if the loader is unavailable, the key itself
    /// is returned rather than throwing in the middle of a binding.
    /// </summary>
    public sealed class Localizer
    {
        private readonly ResourceLoader _loader;

        /// <summary>
        /// Instance used by XAML converters. Assigned once during startup.
        /// </summary>
        public static Localizer Current { get; set; }

        public Localizer()
        {
            try
            {
                _loader = ResourceLoader.GetForCurrentView();
            }
            catch (Exception)
            {
                // No package resource map yet. Get() falls back to the key.
                _loader = null;
            }
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (_loader == null)
            {
                return key;
            }

            try
            {
                var value = _loader.GetString(key);
                return string.IsNullOrEmpty(value) ? key : value;
            }
            catch (Exception)
            {
                return key;
            }
        }

        public string Format(string key, params object[] arguments)
        {
            var template = Get(key);

            if (arguments == null || arguments.Length == 0)
            {
                return template;
            }

            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, arguments);
            }
            catch (FormatException)
            {
                return template;
            }
        }
    }
}
