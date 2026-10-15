using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Wikipedia.Models;
using Wikipedia.Services;

namespace Wikipedia.ViewModels
{
    /// <summary>
    /// Article screen. Content is served cache first so a previously opened
    /// article renders without touching the network, and the fetch path only
    /// runs on a miss.
    /// </summary>
    public sealed class ArticleViewModel : ViewModelBase
    {
        private readonly IWikipediaApi _api;
        private readonly IArticleCache _cache;
        private readonly IHistoryStore _history;
        private readonly ISavedArticleStore _saved;
        private readonly INavigationService _navigation;
        private readonly IConnectivity _connectivity;
        private readonly IShareService _share;
        private readonly ISecondaryTileService _tiles;
        private readonly ISettingsService _settings;
        private readonly ILanguageService _language;
        private readonly Localizer _localizer;

        private string _title;
        private string _summary;
        private string _thumbnailUrl;
        private bool _isSaved;
        private bool _isLoaded;

        public ArticleViewModel(
            IWikipediaApi api,
            IArticleCache cache,
            IHistoryStore history,
            ISavedArticleStore saved,
            INavigationService navigation,
            IConnectivity connectivity,
            IShareService share,
            ISecondaryTileService tiles,
            ISettingsService settings,
            ILanguageService language,
            Localizer localizer)
        {
            if (api == null) throw new ArgumentNullException("api");
            if (cache == null) throw new ArgumentNullException("cache");
            if (history == null) throw new ArgumentNullException("history");
            if (saved == null) throw new ArgumentNullException("saved");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (connectivity == null) throw new ArgumentNullException("connectivity");
            if (share == null) throw new ArgumentNullException("share");
            if (tiles == null) throw new ArgumentNullException("tiles");
            if (settings == null) throw new ArgumentNullException("settings");
            if (language == null) throw new ArgumentNullException("language");
            if (localizer == null) throw new ArgumentNullException("localizer");

            _api = api;
            _cache = cache;
            _history = history;
            _saved = saved;
            _navigation = navigation;
            _connectivity = connectivity;
            _share = share;
            _tiles = tiles;
            _settings = settings;
            _language = language;
            _localizer = localizer;

            Sections = new ObservableCollection<ArticleSection>();

            ToggleSaveCommand = new AsyncRelayCommand(
                parameter => ToggleSaveAsync(),
                parameter => _isLoaded && !IsBusy,
                exception => ErrorMessage = _localizer.Get("Error_Generic"));

            RandomCommand = new AsyncRelayCommand(
                parameter => OpenRandomAsync(),
                parameter => !IsBusy,
                exception => ErrorMessage = DescribeFailure());

            ShareCommand = new RelayCommand(parameter => ShareArticle());
            PinCommand = new AsyncRelayCommand(parameter => PinArticleAsync());
        }

        public ObservableCollection<ArticleSection> Sections { get; private set; }

        public string Title
        {
            get { return _title; }
            private set { SetProperty(ref _title, value); }
        }

        public string Summary
        {
            get { return _summary; }
            private set { SetProperty(ref _summary, value); }
        }

        public string ThumbnailUrl
        {
            get { return _thumbnailUrl; }
            private set
            {
                if (SetProperty(ref _thumbnailUrl, value))
                {
                    RaisePropertyChanged("HasThumbnail");
                }
            }
        }

        public bool HasThumbnail
        {
            get { return !string.IsNullOrEmpty(_thumbnailUrl); }
        }

        public bool IsSaved
        {
            get { return _isSaved; }
            private set
            {
                if (SetProperty(ref _isSaved, value))
                {
                    RaisePropertyChanged("SaveButtonText");
                }
            }
        }

        public string SaveButtonText
        {
            get { return _localizer.Get(_isSaved ? "Article_Unsave" : "Article_Save"); }
        }

        public string SectionsHeader
        {
            get { return _localizer.Get("Article_Sections"); }
        }

        /// <summary>
        /// Font size for the article body. The index comes from settings so
        /// the reader can make long articles comfortable to read.
        /// </summary>
        public double BodyFontSize
        {
            get
            {
                switch (_settings.FontScale)
                {
                    case 0:
                        return 15.0;
                    case 2:
                        return 21.0;
                    default:
                        return 18.0;
                }
            }
        }

        public AsyncRelayCommand ToggleSaveCommand { get; private set; }
        public AsyncRelayCommand RandomCommand { get; private set; }
        public RelayCommand ShareCommand { get; private set; }
        public AsyncRelayCommand PinCommand { get; private set; }

        /// <summary>
        /// Loads an article. Called from OnNavigatedTo, so it never lets an
        /// exception escape: failures land in <see cref="ViewModelBase.ErrorMessage"/>.
        /// </summary>
        public async Task LoadAsync(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                ErrorMessage = _localizer.Get("Error_NotFound");
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var article = await _cache.TryGetAsync(title, CancellationToken.None);

                if (article == null)
                {
                    if (!_connectivity.IsInternetAvailable())
                    {
                        ErrorMessage = _localizer.Get("Error_Offline");
                        return;
                    }

                    article = await _api.GetArticleAsync(title, CancellationToken.None);

                    if (article != null)
                    {
                        await _cache.PutAsync(article, CancellationToken.None);
                    }
                }

                if (article == null)
                {
                    ErrorMessage = _localizer.Get("Error_NotFound");
                    return;
                }

                Apply(article);

                IsSaved = await _saved.IsSavedAsync(article.Title, CancellationToken.None);

                await RecordHistoryAsync(article);
            }
            catch (WikipediaApiException)
            {
                ErrorMessage = _localizer.Get("Error_NotFound");
            }
            catch (Exception)
            {
                ErrorMessage = _localizer.Get("Error_Generic");
            }
            finally
            {
                IsBusy = false;
                _isLoaded = true;
                ToggleSaveCommand.RaiseCanExecuteChanged();
            }
        }

        private void Apply(ArticleContent article)
        {
            Title = article.Title;
            Summary = article.Summary;
            ThumbnailUrl = article.ThumbnailUrl;

            Sections.Clear();

            if (article.Sections != null)
            {
                foreach (var section in article.Sections)
                {
                    Sections.Add(section);
                }
            }

            RaisePropertyChanged("BodyFontSize");
        }

        private async Task RecordHistoryAsync(ArticleContent article)
        {
            if (!_settings.IsHistoryEnabled)
            {
                return;
            }

            try
            {
                await _history.AddAsync(
                    new HistoryEntry
                    {
                        Title = article.Title,
                        ThumbnailUrl = article.ThumbnailUrl,
                        ViewedAt = DateTimeOffset.Now
                    },
                    CancellationToken.None);
            }
            catch (Exception)
            {
                // History is a convenience, never a reason to fail the load.
            }
        }

        private async Task ToggleSaveAsync()
        {
            if (string.IsNullOrEmpty(Title))
            {
                return;
            }

            try
            {
                if (IsSaved)
                {
                    await _saved.RemoveAsync(Title, CancellationToken.None);
                    IsSaved = false;
                    return;
                }

                // Saving keeps the full text so the entry works offline.
                var cached = await _cache.TryGetAsync(Title, CancellationToken.None);

                var article = cached ?? await _api.GetArticleAsync(Title, CancellationToken.None);

                if (article == null)
                {
                    ErrorMessage = _localizer.Get("Error_NotFound");
                    return;
                }

                await _saved.SaveAsync(
                    new SavedArticle
                    {
                        Title = article.Title,
                        ThumbnailUrl = article.ThumbnailUrl,
                        PlainText = article.PlainText,
                        SavedAt = DateTimeOffset.Now
                    },
                    CancellationToken.None);

                IsSaved = true;
            }
            catch (Exception)
            {
                ErrorMessage = _localizer.Get("Error_Generic");
            }
        }

        private async Task OpenRandomAsync()
        {
            if (!_connectivity.IsInternetAvailable())
            {
                ErrorMessage = _localizer.Get("Error_Offline");
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

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
            catch (Exception)
            {
                ErrorMessage = DescribeFailure();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ShareArticle()
        {
            if (string.IsNullOrEmpty(Title))
            {
                return;
            }

            _share.ShareArticle(Title, _language.ArticleUrl(Title));
        }

        private async Task PinArticleAsync()
        {
            if (string.IsNullOrEmpty(Title))
            {
                return;
            }

            try
            {
                var pinned = await _tiles.PinArticleAsync(Title, Title);

                if (!pinned)
                {
                    ErrorMessage = _localizer.Get("Error_PinFailed");
                }
            }
            catch (Exception)
            {
                ErrorMessage = _localizer.Get("Error_PinFailed");
            }
        }

        private string DescribeFailure()
        {
            return _connectivity.IsInternetAvailable()
                ? _localizer.Get("Error_Generic")
                : _localizer.Get("Error_Offline");
        }
    }
}
