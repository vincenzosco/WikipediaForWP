using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Wikipedia.Models;
using Wikipedia.Services;
using Wikipedia.ViewModels;

namespace Wikipedia.Views
{
    /// <summary>Reading history page.</summary>
    public sealed partial class HistoryPage : Page
    {
        private readonly HistoryViewModel _viewModel;

        public HistoryPage()
        {
            InitializeComponent();

            _viewModel = new HistoryViewModel(
                CompositionRoot.History,
                CompositionRoot.Navigation,
                CompositionRoot.Localizer);

            DataContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            await _viewModel.LoadAsync();
        }

        private void Entries_ItemClick(object sender, ItemClickEventArgs e)
        {
            var entry = e.ClickedItem as HistoryEntry;

            if (entry != null)
            {
                CompositionRoot.Navigation.GoToArticle(entry.Title);
            }
        }
    }
}
