using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Wikipedia.Services;
using Wikipedia.ViewModels;

namespace Wikipedia.Views
{
    /// <summary>
    /// Article page. A fresh instance is created for every navigation, so
    /// state can never leak from the previously opened article.
    /// </summary>
    public sealed partial class ArticlePage : Page
    {
        private readonly ArticleViewModel _viewModel;
        private string _currentTitle;

        public ArticlePage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Disabled;

            _viewModel = new ArticleViewModel(
                CompositionRoot.Api,
                CompositionRoot.Cache,
                CompositionRoot.History,
                CompositionRoot.Saved,
                CompositionRoot.Navigation,
                CompositionRoot.Connectivity,
                new ShareService(CompositionRoot.Localizer),
                new SecondaryTileService(),
                CompositionRoot.Settings,
                CompositionRoot.Language,
                CompositionRoot.Localizer);

            DataContext = _viewModel;

            ApplyReadingDirection();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Re-applied on every navigation: the edition can be changed from
            // the settings screen while this page is off screen.
            ApplyReadingDirection();

            _currentTitle = e.Parameter as string;

            if (!string.IsNullOrEmpty(_currentTitle))
            {
                // LoadAsync reports failures through the view model.
                await _viewModel.LoadAsync(_currentTitle);
            }
        }

        /// <summary>
        /// Lays the article out in the direction its edition is written, so an
        /// Arabic or Hebrew entry reads the way it is written rather than
        /// mirrored. Only the content area is flipped.
        /// </summary>
        private void ApplyReadingDirection()
        {
            ContentRoot.FlowDirection = CompositionRoot.Language.IsRightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }

        private async void Retry_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentTitle))
            {
                await _viewModel.LoadAsync(_currentTitle);
            }
        }
    }
}
