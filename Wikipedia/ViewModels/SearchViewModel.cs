using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Wikipedia.Models;
using Wikipedia.Services;

namespace Wikipedia.ViewModels
{
    /// <summary>Search screen: query entry and the result list.</summary>
    public sealed class SearchViewModel : ViewModelBase
    {
        private const int ResultLimit = 25;

        private readonly IWikipediaApi _api;
        private readonly INavigationService _navigation;
        private readonly IConnectivity _connectivity;
        private readonly Localizer _localizer;

        private string _query;
        private bool _hasSearched;

        public SearchViewModel(
            IWikipediaApi api,
            INavigationService navigation,
            IConnectivity connectivity,
            Localizer localizer)
        {
            if (api == null) throw new ArgumentNullException("api");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (connectivity == null) throw new ArgumentNullException("connectivity");
            if (localizer == null) throw new ArgumentNullException("localizer");

            _api = api;
            _navigation = navigation;
            _connectivity = connectivity;
            _localizer = localizer;

            Results = new ObservableCollection<SearchResult>();

            SearchCommand = new AsyncRelayCommand(
                parameter => SearchAsync(parameter as string),
                parameter => !IsBusy,
                exception => ErrorMessage = DescribeFailure(exception));

            OpenResultCommand = new RelayCommand(OpenResult);
        }

        public ObservableCollection<SearchResult> Results { get; private set; }

        /// <summary>
        /// Bound two way to the search box. Submitting the box passes the
        /// current text through the command parameter, which avoids depending
        /// on the property having already been updated by the binding.
        /// </summary>
        public string Query
        {
            get { return _query; }
            set { SetProperty(ref _query, value); }
        }

        public bool HasSearched
        {
            get { return _hasSearched; }
            private set
            {
                if (SetProperty(ref _hasSearched, value))
                {
                    RaisePropertyChanged("ShowEmptyState");
                }
            }
        }

        /// <summary>True when a search ran and returned nothing.</summary>
        public bool ShowEmptyState
        {
            get { return _hasSearched && Results.Count == 0 && !IsBusy && !HasError; }
        }

        public string EmptyStateText
        {
            get { return _localizer.Get("Search_NoResults"); }
        }

        public AsyncRelayCommand SearchCommand { get; private set; }
        public RelayCommand OpenResultCommand { get; private set; }

        /// <summary>
        /// Runs a search. Never throws: a failure is reported through
        /// <see cref="ViewModelBase.ErrorMessage"/>. That promise is load
        /// bearing, because the page calls this from async void event
        /// handlers, where an escaping exception terminates the process.
        /// </summary>
        public async Task SearchAsync(string query)
        {
            var term = string.IsNullOrEmpty(query) ? Query : query;

            if (string.IsNullOrWhiteSpace(term))
            {
                return;
            }

            Query = term;

            if (!_connectivity.IsInternetAvailable())
            {
                ErrorMessage = _localizer.Get("Error_Offline");
                HasSearched = true;
                RaisePropertyChanged("ShowEmptyState");
                return;
            }

            ErrorMessage = null;
            IsBusy = true;
            RaisePropertyChanged("ShowEmptyState");

            try
            {
                var results = await _api.SearchAsync(term, ResultLimit, CancellationToken.None);

                Results.Clear();

                foreach (var result in results)
                {
                    Results.Add(result);
                }

                HasSearched = true;
            }
            catch (Exception exception)
            {
                // Both entry points on the page are async void handlers, and an
                // exception that escapes one of those terminates the process.
                // Without this clause any failed request took the app down.
                // The previous results go with it: they answer a different
                // query than the one now in the box, so leaving them on screen
                // under an error would be misleading.
                Results.Clear();
                ErrorMessage = DescribeFailure(exception);
            }
            finally
            {
                IsBusy = false;
                RaisePropertyChanged("ShowEmptyState");
            }
        }

        private void OpenResult(object parameter)
        {
            var result = parameter as SearchResult;

            if (result != null)
            {
                _navigation.GoToArticle(result.Title);
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
