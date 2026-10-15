using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Wikipedia.Models;
using Wikipedia.Services;

namespace Wikipedia.ViewModels
{
    /// <summary>
    /// Settings screen: which Wikipedia edition to read, reading text size,
    /// the history switch and the two maintenance actions.
    ///
    /// The edition is a content setting, kept apart from the interface
    /// language. Choosing one therefore applies to the next request and never
    /// needs the app to restart.
    /// </summary>
    public sealed class SettingsViewModel : ViewModelBase
    {
        /// <summary>
        /// Code carried by the entry that clears the override. No Wikipedia
        /// edition uses an empty subdomain, so it cannot collide with one.
        /// </summary>
        private const string FollowDeviceCode = "";

        private readonly ILanguageService _language;
        private readonly ILanguageCatalog _catalog;
        private readonly ISettingsService _settings;
        private readonly IHistoryStore _history;
        private readonly IArticleCache _cache;
        private readonly Localizer _localizer;

        /// <summary>Every edition, before the search box narrows it down.</summary>
        private readonly List<WikiLanguage> _allLanguages;

        private int _fontScaleIndex;
        private bool _isHistoryEnabled;
        private string _statusMessage;
        private string _languageFilter;
        private WikiLanguage _selectedLanguage;
        private bool _isListIncomplete;
        private bool _hasLoadedLanguages;

        public SettingsViewModel(
            ILanguageService language,
            ILanguageCatalog catalog,
            ISettingsService settings,
            IHistoryStore history,
            IArticleCache cache,
            Localizer localizer)
        {
            if (language == null) throw new ArgumentNullException("language");
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (settings == null) throw new ArgumentNullException("settings");
            if (history == null) throw new ArgumentNullException("history");
            if (cache == null) throw new ArgumentNullException("cache");
            if (localizer == null) throw new ArgumentNullException("localizer");

            _language = language;
            _catalog = catalog;
            _settings = settings;
            _history = history;
            _cache = cache;
            _localizer = localizer;

            _allLanguages = new List<WikiLanguage>();
            Languages = new ObservableCollection<WikiLanguage>();

            _fontScaleIndex = settings.FontScale;
            _isHistoryEnabled = settings.IsHistoryEnabled;

            ClearHistoryCommand = new AsyncRelayCommand(parameter => ClearHistoryAsync());
            ClearCacheCommand = new AsyncRelayCommand(parameter => ClearCacheAsync());
        }

        /// <summary>Editions matching the search text, in name order.</summary>
        public ObservableCollection<WikiLanguage> Languages { get; private set; }

        /// <summary>
        /// Narrows <see cref="Languages"/> to what the user typed. The box is
        /// driven from the page rather than from the binding so the list reacts
        /// to every keystroke on this XAML version.
        /// </summary>
        public string LanguageFilter
        {
            get { return _languageFilter; }
            set
            {
                if (SetProperty(ref _languageFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// The edition being read. Picking one stores it straight away: the
        /// reading language is a content setting, so nothing has to restart.
        /// </summary>
        public WikiLanguage SelectedLanguage
        {
            get { return _selectedLanguage; }
            set
            {
                // Narrowing the list can push a null through the two way
                // binding when the selected row is filtered out. That is not a
                // choice, so it must not clear the language.
                if (value == null || !SetProperty(ref _selectedLanguage, value))
                {
                    return;
                }

                _language.SetLanguage(value.Code);

                StatusMessage = _localizer.Format("Settings_LanguageChanged", value.Name);

                RaisePropertyChanged("EffectiveLanguageText");
            }
        }

        /// <summary>True when the edition list could not be downloaded.</summary>
        public bool IsListIncomplete
        {
            get { return _isListIncomplete; }
        }

        /// <summary>True once the list has been built, empty filters included.</summary>
        public bool HasLoadedLanguages
        {
            get { return _hasLoadedLanguages; }
        }

        /// <summary>
        /// True when the search text matches nothing. Gated on the list having
        /// loaded, so the empty state cannot flash before the first request.
        /// </summary>
        public bool ShowNoMatch
        {
            get { return _hasLoadedLanguages && Languages.Count == 0; }
        }

        /// <summary>0 = small, 1 = normal, 2 = large.</summary>
        public int FontScaleIndex
        {
            get { return _fontScaleIndex; }
            set
            {
                if (SetProperty(ref _fontScaleIndex, value))
                {
                    _settings.FontScale = value;
                }
            }
        }

        public bool IsHistoryEnabled
        {
            get { return _isHistoryEnabled; }
            set
            {
                if (SetProperty(ref _isHistoryEnabled, value))
                {
                    _settings.IsHistoryEnabled = value;
                }
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set
            {
                if (SetProperty(ref _statusMessage, value))
                {
                    RaisePropertyChanged("HasStatus");
                }
            }
        }

        public bool HasStatus
        {
            get { return !string.IsNullOrEmpty(_statusMessage); }
        }

        public string EffectiveLanguageText
        {
            get { return _localizer.Format("Settings_CurrentLanguage", DisplayNameFor(_language.WikiLanguage)); }
        }

        public AsyncRelayCommand ClearHistoryCommand { get; private set; }
        public AsyncRelayCommand ClearCacheCommand { get; private set; }

        /// <summary>
        /// Fills the picker. Called when the page appears, because the list is
        /// a network request and is not worth starting for a page the user
        /// never opened. A second call is free.
        /// </summary>
        public async Task LoadLanguagesAsync()
        {
            if (_hasLoadedLanguages || IsBusy)
            {
                return;
            }

            IsBusy = true;

            try
            {
                var languages = await _catalog.GetLanguagesAsync(CancellationToken.None);

                _allLanguages.Clear();

                // The empty code is the entry that clears the override, so
                // following the device stays reachable without a second switch
                // on the page.
                _allLanguages.Add(new WikiLanguage(
                    FollowDeviceCode,
                    _localizer.Get("Settings_FollowDevice"),
                    false));

                foreach (var language in languages)
                {
                    _allLanguages.Add(language);
                }

                _isListIncomplete = _catalog.IsFallback;
                _hasLoadedLanguages = true;

                ApplyFilter();
                SelectCurrentLanguage();
            }
            catch (Exception)
            {
                // This runs from OnNavigatedTo, where an exception that escapes
                // terminates the process. An empty picker is the acceptable
                // worst case, and the list is retried on the next visit
                // because the loaded flag stays unset.
                ErrorMessage = _localizer.Get("Error_Generic");
            }
            finally
            {
                IsBusy = false;

                RaisePropertyChanged("IsListIncomplete");
                RaisePropertyChanged("HasLoadedLanguages");
                RaisePropertyChanged("ShowNoMatch");
                RaisePropertyChanged("EffectiveLanguageText");
            }
        }

        /// <summary>
        /// Puts the picker on the edition being read: the stored choice when
        /// there is one, and the follow device entry otherwise.
        /// </summary>
        private void SelectCurrentLanguage()
        {
            var chosen = _language.HasChosenLanguage ? _language.WikiLanguage : FollowDeviceCode;

            foreach (var language in _allLanguages)
            {
                if (!string.Equals(language.Code, chosen, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Assigned through the backing field: this is the state the
                // picker opens in, not a choice the user just made, so it must
                // not be written back or announced.
                _selectedLanguage = language;
                RaisePropertyChanged("SelectedLanguage");
                return;
            }
        }

        private void ApplyFilter()
        {
            var filter = _languageFilter == null ? string.Empty : _languageFilter.Trim();

            Languages.Clear();

            foreach (var language in _allLanguages)
            {
                if (Matches(language, filter))
                {
                    Languages.Add(language);
                }
            }

            RaisePropertyChanged("ShowNoMatch");

            // Rebuilding the collection drops the selection on the control, so
            // the current value is pushed back to it. The setter ignores null,
            // which is what that drop sends the other way.
            RaisePropertyChanged("SelectedLanguage");
        }

        private static bool Matches(WikiLanguage language, string filter)
        {
            if (filter.Length == 0)
            {
                return true;
            }

            // A reader looking for an edition types either its own name, which
            // is the one they recognise, or the code they know from a URL.
            return language.Name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0
                || language.Code.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The name of an edition, or the bare code while the list is still
        /// loading or when it holds nothing for that code.
        /// </summary>
        private string DisplayNameFor(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return _localizer.Get("Settings_FollowDevice");
            }

            foreach (var language in _allLanguages)
            {
                if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return language.Name;
                }
            }

            return code;
        }

        private async Task ClearHistoryAsync()
        {
            try
            {
                await _history.ClearAsync(CancellationToken.None);
                StatusMessage = _localizer.Get("Settings_HistoryCleared");
            }
            catch (Exception)
            {
                ErrorMessage = _localizer.Get("Error_Generic");
            }
        }

        private async Task ClearCacheAsync()
        {
            try
            {
                await _cache.ClearAsync(CancellationToken.None);
                StatusMessage = _localizer.Get("Settings_CacheCleared");
            }
            catch (Exception)
            {
                ErrorMessage = _localizer.Get("Error_Generic");
            }
        }
    }
}
