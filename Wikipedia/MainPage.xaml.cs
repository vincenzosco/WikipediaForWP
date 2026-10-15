using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Wikipedia.Models;
using Wikipedia.Services;
using Wikipedia.ViewModels;

namespace Wikipedia
{
    /// <summary>Home page.</summary>
    public sealed partial class MainPage : Page
    {
        private readonly MainViewModel _viewModel;

        public MainPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Required;

            _viewModel = new MainViewModel(
                CompositionRoot.Api,
                CompositionRoot.History,
                CompositionRoot.Navigation,
                CompositionRoot.Connectivity,
                CompositionRoot.Localizer);

            DataContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // LoadAsync handles its own failures, so nothing escapes here.
            await _viewModel.LoadAsync();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            CompositionRoot.Navigation.GoToSearch();
        }

        private void Saved_Click(object sender, RoutedEventArgs e)
        {
            CompositionRoot.Navigation.GoToSaved();
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            CompositionRoot.Navigation.GoToHistory();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            CompositionRoot.Navigation.GoToSettings();
        }

        private void Recent_ItemClick(object sender, ItemClickEventArgs e)
        {
            var entry = e.ClickedItem as HistoryEntry;

            if (entry != null)
            {
                CompositionRoot.Navigation.GoToArticle(entry.Title);
            }
        }
    }
}
