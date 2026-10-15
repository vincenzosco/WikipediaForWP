using System;

namespace Wikipedia.Services
{
    /// <summary>
    /// Single place where the service graph is assembled. Every service is
    /// created on first use, so nothing is built that the first page does not
    /// need, and construction cost stays off the launch path.
    ///
    /// View models take their dependencies through their constructors and
    /// never reach back into this class.
    /// </summary>
    public static class CompositionRoot
    {
        private static readonly Lazy<ILanguageService> LanguageInstance =
            new Lazy<ILanguageService>(() => new LanguageService(Settings));

        private static readonly Lazy<IWikipediaApi> ApiInstance =
            new Lazy<IWikipediaApi>(() => new WikipediaApi(Language));

        // The per edition stores take the language service so a title is never
        // read back as the article from a different Wikipedia.
        private static readonly Lazy<IArticleCache> CacheInstance =
            new Lazy<IArticleCache>(() => new ArticleCache(Language));

        private static readonly Lazy<ISavedArticleStore> SavedInstance =
            new Lazy<ISavedArticleStore>(() => new SavedArticleStore(Language));

        private static readonly Lazy<IHistoryStore> HistoryInstance =
            new Lazy<IHistoryStore>(() => new HistoryStore(Language));

        private static readonly Lazy<ISettingsService> SettingsInstance =
            new Lazy<ISettingsService>(() => new SettingsService());

        private static readonly Lazy<IConnectivity> ConnectivityInstance =
            new Lazy<IConnectivity>(() => new ConnectivityService());

        private static readonly Lazy<ILanguageCatalog> CatalogInstance =
            new Lazy<ILanguageCatalog>(() => new LanguageCatalog(Connectivity));

        /// <summary>
        /// Assigned during startup once the root Frame exists, because the
        /// navigation service needs a frame to drive.
        /// </summary>
        public static INavigationService Navigation { get; set; }

        /// <summary>Set during startup so XAML converters can resolve strings.</summary>
        public static Localizer Localizer { get; set; }

        public static ILanguageService Language
        {
            get { return LanguageInstance.Value; }
        }

        public static IWikipediaApi Api
        {
            get { return ApiInstance.Value; }
        }

        public static IArticleCache Cache
        {
            get { return CacheInstance.Value; }
        }

        public static ISavedArticleStore Saved
        {
            get { return SavedInstance.Value; }
        }

        public static IHistoryStore History
        {
            get { return HistoryInstance.Value; }
        }

        public static ISettingsService Settings
        {
            get { return SettingsInstance.Value; }
        }

        public static IConnectivity Connectivity
        {
            get { return ConnectivityInstance.Value; }
        }

        /// <summary>Edition list behind the language picker.</summary>
        public static ILanguageCatalog Languages
        {
            get { return CatalogInstance.Value; }
        }
    }
}
