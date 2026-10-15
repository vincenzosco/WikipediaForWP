using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Wikipedia.Models;
using Wikipedia.Services;

namespace Wikipedia.ViewModels
{
    /// <summary>
    /// Home screen: the entry point to search, a shortcut to a random
    /// article, and the most recently read articles.
    /// </summary>
    public sealed class MainViewModel : ViewModelBase
    {
        private const int RecentCount = 5;

        private readonly IWikipediaApi _api;
        private readonly IHistoryStore _history;
        private readonly INavigationService _navigation;
        private readonly IConnectivity _connectivity;
        private readonly Localizer _localizer;

        public MainViewModel(
            IWikipediaApi api,
            IHistoryStore history,
            INavigationService navigation,
            IConnectivity connectivity,
            Localizer localizer)
        {
            if (api == null) throw new ArgumentNullException("api");
            if (history == null) throw new ArgumentNullException("history");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (connectivity == null) throw new ArgumentNullException("connectivity");
            if (localizer == null) throw new ArgumentNullException("localizer");

            _api = api;
            _history = history;
            _navigation = navigation;
            _connectivity = connectivity;
            _localizer = localizer;

            Recent = new ObservableCollection<HistoryEntry>();

            SearchCommand = new RelayCommand(parameter => _navigation.GoToSearch());
            SavedCommand = new RelayCommand(parameter => _navigation.GoToSaved());
            HistoryCommand = new RelayCommand(parameter => _navigation.GoToHistory());
            SettingsCommand = new RelayCommand(parameter => _navigation.GoToSettings());
            OpenRecentCommand = new RelayCommand(OpenRecent);

            RandomCommand = new AsyncRelayCommand(
                parameter => OpenRandomAsync(),
                parameter => !IsBusy,
                exception => ErrorMessage = DescribeFailure(exception));
        }

        public ObservableCollection<HistoryEntry> Recent { get; private set; }

        public bool HasRecent
        {
            get { return Recent.Count > 0; }
        }

        public RelayCommand SearchCommand { get; private set; }
        public RelayCommand SavedCommand { get; private set; }
        public RelayCommand HistoryCommand { get; private set; }
        public RelayCommand SettingsCommand { get; private set; }
        public RelayCommand OpenRecentCommand { get; private set; }
        public AsyncRelayCommand RandomCommand { get; private set; }

        /// <summary>
        /// Refreshes the recent list. Never throws: the page calls this from
        /// OnNavigatedTo, where an escaping exception would take the app down.
        /// </summary>
        public async Task LoadAsync()
        {
            try
            {
                var entries = await _history.GetAllAsync(CancellationToken.None);

                Recent.Clear();

                for (var index = 0; index < entries.Count && index < RecentCount; index++)
                {
                    Recent.Add(entries[index]);
                }

                RaisePropertyChanged("HasRecent");
            }
            catch (Exception)
            {
                // An unreadable history simply leaves the section empty.
            }
        }

        private void OpenRecent(object parameter)
        {
            var entry = parameter as HistoryEntry;

            if (entry != null)
            {
                _navigation.GoToArticle(entry.Title);
            }
        }

        private async Task OpenRandomAsync()
        {
            if (!_connectivity.IsInternetAvailable())
            {
                ErrorMessage = _localizer.Get("Error_Offline");
                return;
            }

            ErrorMessage = null;
            IsBusy = true;

            try
            {
                var article = await _api.GetRandomArticleAsync(CancellationToken.None);

                if (article != null && !string.IsNullOrEmpty(article.Title))
                {
                    _navigation.GoToArticle(article.Title);
                }
                else
                {
                    ErrorMessage = _localizer.Get("Error_NotFound");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string DescribeFailure(Exception exception)
        {
            return _connectivity.IsInternetAvailable()
                ? _localizer.Get("Error_Generic")
                : _localizer.Get("Error_Offline");
        }
    }
}
