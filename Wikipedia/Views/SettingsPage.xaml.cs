using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Wikipedia.Services;
using Wikipedia.ViewModels;

namespace Wikipedia.Views
{
    /// <summary>Settings page.</summary>
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsPage()
        {
            InitializeComponent();

            _viewModel = new SettingsViewModel(
                CompositionRoot.Language,
                CompositionRoot.Languages,
                CompositionRoot.Settings,
                CompositionRoot.History,
                CompositionRoot.Cache,
                CompositionRoot.Localizer);

            DataContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Started here rather than in the constructor: the edition list is
            // a network request, so it is only worth starting for a page that
            // is actually on screen.
            await _viewModel.LoadLanguagesAsync();
        }

        /// <summary>
        /// The filter is pushed from the box on every keystroke instead of
        /// through a binding, which on this XAML version waits for focus to
        /// leave the field before it updates the source.
        /// </summary>
        private void LanguageSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.LanguageFilter = LanguageSearchBox.Text;
        }
    }
}
