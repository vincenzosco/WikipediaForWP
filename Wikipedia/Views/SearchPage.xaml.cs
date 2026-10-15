using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Wikipedia.Models;
using Wikipedia.Services;
using Wikipedia.ViewModels;

namespace Wikipedia.Views
{
    /// <summary>Search page.</summary>
    public sealed partial class SearchPage : Page
    {
        private readonly SearchViewModel _viewModel;

        public SearchPage()
        {
            InitializeComponent();

            _viewModel = new SearchViewModel(
                CompositionRoot.Api,
                CompositionRoot.Navigation,
                CompositionRoot.Connectivity,
                CompositionRoot.Localizer);

            DataContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Results are titles from the edition being read, so the list and
            // the query follow its direction. Applied here because this page
            // is cached and the edition can change while it is off screen.
            ContentRoot.FlowDirection = CompositionRoot.Language.IsRightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            QueryTextBox.Focus(FocusState.Programmatic);
        }

        private void QueryTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            e.Handled = true;

            // Routed through the command rather than calling SearchAsync
            // directly: the API failure SearchAsync lets through is reported
            // by the command's error handler, whereas from this handler it
            // would escape as an unhandled exception and terminate the app.
            _viewModel.SearchCommand.Execute(QueryTextBox.Text);
        }

        private void Results_ItemClick(object sender, ItemClickEventArgs e)
        {
            var result = e.ClickedItem as SearchResult;

            if (result != null)
            {
                CompositionRoot.Navigation.GoToArticle(result.Title);
            }
        }
    }
}
