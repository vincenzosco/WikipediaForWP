using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Wikipedia.Models;

namespace Wikipedia.Services
{
    /// <summary>Every Wikipedia edition the app can read.</summary>
    public interface ILanguageCatalog
    {
        /// <summary>
        /// True when the list came from the built in fallback instead of the
        /// network or the cache, so the picker can say it is incomplete.
        /// </summary>
        bool IsFallback { get; }

        Task<IReadOnlyList<WikiLanguage>> GetLanguagesAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// The list of editions, downloaded once and then kept on disk.
    ///
    /// A single request carries both halves of what the picker needs: the
    /// siteinfo meta lists the editions that actually have content, and the
    /// languageinfo meta adds the writing direction, which no other part of
    /// the API exposes and which cannot be guessed from the code. The list is
    /// cached because it changes rarely and the picker has to work offline.
    /// </summary>
    public sealed class LanguageCatalog : ILanguageCatalog
    {
        private const string FileName = "languages.json";

        private const string Url =
            "https://en.wikipedia.org/w/api.php?action=query" +
            "&meta=siteinfo|languageinfo&siprop=languages&liprop=dir" +
            "&format=json&formatversion=2&uselang=en";

        /// <summary>Wire format, owned by the API.</summary>
        private const string QueryKey = "query";
        private const string LanguagesKey = "languages";
        private const string InfoKey = "languageinfo";
        private const string CodeKey = "code";
        private const string NameKey = "name";
        private const string DirectionKey = "dir";
        private const string RightToLeftValue = "rtl";

        /// <summary>On disk format, owned by this app.</summary>
        private const string StoredKey = "languages";
        private const string StoredCodeKey = "code";
        private const string StoredNameKey = "name";
        private const string StoredRtlKey = "rtl";

        private readonly IConnectivity _connectivity;
        private readonly HttpClient _client;

        private IReadOnlyList<WikiLanguage> _loaded;
        private bool _isFallback;

        public LanguageCatalog(IConnectivity connectivity)
        {
            if (connectivity == null)
            {
                throw new ArgumentNullException("connectivity");
            }

            _connectivity = connectivity;
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(30);

            try
            {
                _client.DefaultRequestHeaders.UserAgent.ParseAdd(WikipediaApi.UserAgent);
                _client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            }
            catch (FormatException)
            {
                // A malformed header must not stop the picker from opening.
            }
        }

        public bool IsFallback
        {
            get { return _isFallback; }
        }

        public async Task<IReadOnlyList<WikiLanguage>> GetLanguagesAsync(CancellationToken cancellationToken)
        {
            if (_loaded != null)
            {
                return _loaded;
            }

            var stored = await ReadStoredAsync();

            if (stored != null)
            {
                _loaded = stored;
                return _loaded;
            }

            var downloaded = await DownloadAsync(cancellationToken);

            if (downloaded != null)
            {
                _loaded = downloaded;
                await WriteStoredAsync(downloaded);
                return _loaded;
            }

            _isFallback = true;
            _loaded = BuildFallback();
            return _loaded;
        }

        /// <summary>
        /// Last resort when the list was never downloaded and the device is
        /// offline. The caller tells the user the list is incomplete, so these
        /// entries only have to be readable.
        /// </summary>
        private static IReadOnlyList<WikiLanguage> BuildFallback()
        {
            var languages = new List<WikiLanguage>();

            AddFallback(languages, LanguageService.ResolveDeviceLanguage());
            AddFallback(languages, LanguageService.Italian);
            AddFallback(languages, LanguageService.English);

            return languages;
        }

        private static void AddFallback(List<WikiLanguage> languages, string code)
        {
            if (string.IsNullOrEmpty(code) || Contains(languages, code))
            {
                return;
            }

            languages.Add(new WikiLanguage(code, code, LanguageService.IsRightToLeftCode(code)));
        }

        private static bool Contains(List<WikiLanguage> languages, string code)
        {
            foreach (var language in languages)
            {
                if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<IReadOnlyList<WikiLanguage>> DownloadAsync(CancellationToken cancellationToken)
        {
            if (!_connectivity.IsInternetAvailable())
            {
                return null;
            }

            try
            {
                using (var response = await _client.GetAsync(Url, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var payload = await response.Content.ReadAsStringAsync();

                    return Parse(payload);
                }
            }
            catch (OperationCanceledException)
            {
                // Only a caller that actually cancelled gets the cancellation.
                // A request that ran past the client timeout arrives here too,
                // and that is a failed download rather than a cancellation, so
                // it falls through to the stored list and the built in
                // fallback the way any other network failure does.
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return null;
            }
            catch (Exception)
            {
                // The caller falls back to the stored list, and then to the
                // handful of languages the app is guaranteed to ship with.
                return null;
            }
        }

        private static List<WikiLanguage> Parse(string payload)
        {
            var root = JsonIo.Parse(payload);
            var query = JsonIo.ReadObject(root, QueryKey);
            var editions = JsonIo.ReadArray(query, LanguagesKey);

            if (editions == null)
            {
                return null;
            }

            var info = JsonIo.ReadObject(query, InfoKey);
            var languages = new List<WikiLanguage>();

            foreach (var edition in editions)
            {
                if (edition.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                var entry = edition.GetObject();
                var code = JsonIo.ReadString(entry, CodeKey);

                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                var name = JsonIo.ReadString(entry, NameKey);

                if (string.IsNullOrEmpty(name))
                {
                    name = code;
                }

                languages.Add(new WikiLanguage(code, name, IsRightToLeft(info, code)));
            }

            if (languages.Count == 0)
            {
                return null;
            }

            languages.Sort(CompareByName);

            return languages;
        }

        /// <summary>
        /// The direction only comes from the languageinfo meta, which is keyed
        /// by language code rather than being an array.
        /// </summary>
        private static bool IsRightToLeft(JsonObject info, string code)
        {
            var direction = JsonIo.ReadString(JsonIo.ReadObject(info, code), DirectionKey);

            return string.Equals(direction, RightToLeftValue, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareByName(WikiLanguage left, WikiLanguage right)
        {
            return string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        private static Task<StorageFile> GetFileAsync()
        {
            return ApplicationData.Current.LocalFolder.TryGetFileAsync(FileName);
        }

        private static async Task<IReadOnlyList<WikiLanguage>> ReadStoredAsync()
        {
            try
            {
                var file = await GetFileAsync();

                if (file == null)
                {
                    return null;
                }

                var root = JsonIo.Parse(await FileIO.ReadTextAsync(file));
                var array = JsonIo.ReadArray(root, StoredKey);

                if (array == null)
                {
                    return null;
                }

                var languages = new List<WikiLanguage>();

                foreach (var item in array)
                {
                    if (item.ValueType != JsonValueType.Object)
                    {
                        continue;
                    }

                    var entry = item.GetObject();
                    var code = JsonIo.ReadString(entry, StoredCodeKey);

                    if (string.IsNullOrEmpty(code))
                    {
                        continue;
                    }

                    languages.Add(new WikiLanguage(
                        code,
                        JsonIo.ReadString(entry, StoredNameKey) ?? code,
                        JsonIo.ReadBoolean(entry, StoredRtlKey, false)));
                }

                return languages.Count == 0 ? null : languages;
            }
            catch (Exception)
            {
                // A corrupt cache is a cache miss; the list is re-downloaded.
                return null;
            }
        }

        private static async Task WriteStoredAsync(IReadOnlyList<WikiLanguage> languages)
        {
            try
            {
                var array = new JsonArray();

                foreach (var language in languages)
                {
                    var entry = new JsonObject();
                    JsonIo.SetString(entry, StoredCodeKey, language.Code);
                    JsonIo.SetString(entry, StoredNameKey, language.Name);
                    JsonIo.SetBoolean(entry, StoredRtlKey, language.IsRightToLeft);

                    array.Add(entry);
                }

                var root = new JsonObject();
                root.SetNamedValue(StoredKey, array);

                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    FileName, CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, root.Stringify());
            }
            catch (Exception)
            {
                // Caching is an optimisation; failing to write it is not an error.
            }
        }
    }
}
