using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Wikipedia.Models;
using Wikipedia.Services;
using Wikipedia.ViewModels;

namespace Wikipedia.Views
{
    /// <summary>Saved articles page.</summary>
    public sealed partial class SavedPage : Page
    {
        private readonly SavedViewModel _viewModel;

        public SavedPage()
        {
            InitializeComponent();

            _viewModel = new SavedViewModel(
                CompositionRoot.Saved,
                CompositionRoot.Navigation,
                CompositionRoot.Localizer);

            DataContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // The list changes from the article page, so it is reloaded on
            // every visit rather than cached.
            await _viewModel.LoadAsync();
        }

        private void Articles_ItemClick(object sender, ItemClickEventArgs e)
        {
            var article = e.ClickedItem as SavedArticle;

            if (article != null)
            {
                CompositionRoot.Navigation.GoToArticle(article.Title);
            }
        }
    }
}
