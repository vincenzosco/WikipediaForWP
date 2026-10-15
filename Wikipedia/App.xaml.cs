using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Wikipedia.Services;
using Wikipedia.Views;

namespace Wikipedia
{
    /// <summary>
    /// Application entry point. Sets up the root frame, wires the service
    /// graph, and decides which page to open from the activation arguments.
    /// </summary>
    public sealed partial class App : Application
    {
        private TransitionCollection _transitions;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            var rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.CacheSize = 1;
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];

                Window.Current.Content = rootFrame;
            }

            InitializeServices();

            if (rootFrame.Content == null)
            {
                // The turnstile animation is suppressed for the first
                // navigation and restored once that navigation completes.
                if (rootFrame.ContentTransitions != null)
                {
                    _transitions = new TransitionCollection();

                    foreach (var transition in rootFrame.ContentTransitions)
                    {
                        _transitions.Add(transition);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += RootFrame_FirstNavigated;

                Type pageType;
                object parameter;
                ResolveLaunchTarget(e.Arguments, out pageType, out parameter);

                if (!rootFrame.Navigate(pageType, parameter))
                {
                    throw new Exception("Failed to create the initial page.");
                }
            }

            Window.Current.Activate();
        }

        /// <summary>
        /// Creates the singletons exactly once. The navigation service needs
        /// the root frame to exist, which is why this runs from OnLaunched
        /// rather than from the constructor.
        /// </summary>
        private static void InitializeServices()
        {
            if (CompositionRoot.Navigation == null)
            {
                CompositionRoot.Navigation = new FrameNavigationService();
            }

            if (CompositionRoot.Localizer == null)
            {
                CompositionRoot.Localizer = new Localizer();
                Localizer.Current = CompositionRoot.Localizer;
            }
        }

        /// <summary>
        /// A tile pinned from an article carries "article:&lt;title&gt;" as its
        /// activation argument, so the app opens straight into that article.
        /// Anything else lands on the home page.
        /// </summary>
        private static void ResolveLaunchTarget(string arguments, out Type pageType, out object parameter)
        {
            if (!string.IsNullOrEmpty(arguments) &&
                arguments.StartsWith(SecondaryTileService.ArgumentPrefix, StringComparison.Ordinal))
            {
                var title = arguments.Substring(SecondaryTileService.ArgumentPrefix.Length);

                if (!string.IsNullOrEmpty(title))
                {
                    pageType = typeof(ArticlePage);
                    parameter = title;
                    return;
                }
            }

            pageType = typeof(MainPage);
            parameter = null;
        }

        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var frame = sender as Frame;

            if (frame != null)
            {
                frame.ContentTransitions = _transitions ?? new TransitionCollection { new NavigationThemeTransition() };
                frame.Navigated -= RootFrame_FirstNavigated;
            }
        }

        /// <summary>
        /// The process can be terminated shortly after this returns, and the
        /// handler is not guaranteed to run at all, so nothing here is load
        /// bearing: every change is already written to storage as it happens.
        /// </summary>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            deferral.Complete();
        }
    }
}
