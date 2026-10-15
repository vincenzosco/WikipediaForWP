using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Wikipedia.Views;

namespace Wikipedia.Services
{
    /// <summary>
    /// Navigation expressed as intentions rather than page types, so view
    /// models never reference a Page class or a route string. Screen names
    /// stay in one place and renaming a page is a change in this file only.
    /// </summary>
    public interface INavigationService
    {
        bool CanGoBack { get; }
        void GoBack();
        void GoToSearch();
        void GoToArticle(string title);
        void GoToSaved();
        void GoToHistory();
        void GoToSettings();
    }

    /// <summary>
    /// Drives the root Frame. WinRT navigation is type based and carries an
    /// object parameter, and the destination page receives it through
    /// NavigationEventArgs.Parameter.
    /// </summary>
    public sealed class FrameNavigationService : INavigationService
    {
        public bool CanGoBack
        {
            get
            {
                var frame = RootFrame;
                return frame != null && frame.CanGoBack;
            }
        }

        public void GoBack()
        {
            var frame = RootFrame;

            if (frame != null && frame.CanGoBack)
            {
                frame.GoBack();
            }
        }

        public void GoToSearch()
        {
            Navigate(typeof(SearchPage), null);
        }

        public void GoToArticle(string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                Navigate(typeof(ArticlePage), title);
            }
        }

        public void GoToSaved()
        {
            Navigate(typeof(SavedPage), null);
        }

        public void GoToHistory()
        {
            Navigate(typeof(HistoryPage), null);
        }

        public void GoToSettings()
        {
            Navigate(typeof(SettingsPage), null);
        }

        private static Frame RootFrame
        {
            get { return Window.Current == null ? null : Window.Current.Content as Frame; }
        }

        private static void Navigate(Type pageType, object parameter)
        {
            var frame = RootFrame;

            if (frame != null)
            {
                frame.Navigate(pageType, parameter);
            }
        }
    }
}
