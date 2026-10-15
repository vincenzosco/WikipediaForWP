using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Wikipedia.Models;

namespace Wikipedia.Services
{
    /// <summary>Read access to a Wikipedia edition.</summary>
    public interface IWikipediaApi
    {
        Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
        Task<ArticleContent> GetArticleAsync(string title, CancellationToken cancellationToken);
        Task<ArticleContent> GetRandomArticleAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Talks to two Wikipedia endpoints and normalizes both into the models
    /// this app binds against.
    ///
    /// Search uses the REST endpoint <c>/w/rest.php/v1/search/page</c>, which
    /// already returns a flat array with a description and a thumbnail.
    /// Article content uses the action API with
    /// <c>prop=extracts|pageimages&amp;explaintext=1&amp;exsectionformat=wiki</c>.
    ///
    /// Plain text is used on purpose. The REST HTML endpoint returns roughly
    /// 1.3 MB for a large article, which is both heavy to cache on a phone and
    /// awkward to hand to WebView.NavigateToString, while the plain text
    /// extract for the same article is about 130 KB and carries the section
    /// headings the table of contents needs.
    /// </summary>
    public sealed class WikipediaApi : IWikipediaApi
    {
        /// <summary>
        /// Wikimedia asks every client to identify itself. The contact URL is
        /// what lets them reach the maintainer instead of blocking the app.
        /// </summary>
        internal const string UserAgent =
            "WikipediaForWP/1.0 (https://github.com/vincenzosco/WikipediaForWP)";

        private const int ThumbnailSize = 400;

        private readonly ILanguageService _language;
        private readonly HttpClient _client;

        public WikipediaApi(ILanguageService language)
        {
            if (language == null)
            {
                throw new ArgumentNullException("language");
            }

            _language = language;
            _client = CreateClient();
        }

        public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
        {
            var results = new List<SearchResult>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            var url = string.Format(
                "{0}?q={1}&limit={2}",
                _language.SearchBaseUrl,
                Uri.EscapeDataString(query.Trim()),
                limit);

            var payload = await GetStringAsync(url, cancellationToken);
            var root = ParseObject(payload);
            var pages = JsonIo.ReadArray(root, "pages");

            if (pages == null)
            {
                return results;
            }

            foreach (var item in pages)
            {
                if (item.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                var page = item.GetObject();
                var title = ReadString(page, "title");

                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                results.Add(new SearchResult
                {
                    PageId = JsonIo.ReadNumber(page, "id", 0),
                    Title = title,
                    Description = ReadString(page, "description"),
                    Excerpt = WikiTextParser.StripHtml(ReadString(page, "excerpt")),
                    ThumbnailUrl = ReadSearchThumbnail(page),
                    Url = _language.ArticleUrl(title)
                });
            }

            return results;
        }

        public Task<ArticleContent> GetArticleAsync(string title, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A title is required.", "title");
            }

            var url = string.Format(
                "{0}?action=query&prop=extracts|pageimages&explaintext=1&exsectionformat=wiki" +
                "&piprop=thumbnail&pithumbsize={1}&redirects=1&format=json&formatversion=2&titles={2}",
                _language.ApiBaseUrl,
                ThumbnailSize,
                Uri.EscapeDataString(title.Trim()));

            return FetchArticleAsync(url, cancellationToken);
        }

        public Task<ArticleContent> GetRandomArticleAsync(CancellationToken cancellationToken)
        {
            // generator=random resolves a random article and its content in a
            // single round trip, so no second request is needed.
            var url = string.Format(
                "{0}?action=query&generator=random&grnnamespace=0&grnlimit=1" +
                "&prop=extracts|pageimages&explaintext=1&exsectionformat=wiki" +
                "&piprop=thumbnail&pithumbsize={1}&format=json&formatversion=2",
                _language.ApiBaseUrl,
                ThumbnailSize);

            return FetchArticleAsync(url, cancellationToken);
        }

        private async Task<ArticleContent> FetchArticleAsync(string url, CancellationToken cancellationToken)
        {
            var payload = await GetStringAsync(url, cancellationToken);
            var root = ParseObject(payload);

            var query = JsonIo.ReadObject(root, "query");

            if (query == null)
            {
                throw new WikipediaApiException("The Wikipedia response did not contain any article data.");
            }

            var pages = JsonIo.ReadArray(query, "pages");

            if (pages == null || pages.Count == 0)
            {
                throw new WikipediaApiException("The requested article could not be found.");
            }

            if (pages[0].ValueType != JsonValueType.Object)
            {
                throw new WikipediaApiException("The requested article could not be found.");
            }

            var page = pages[0].GetObject();

            // A title that does not exist comes back without a page id.
            if (JsonIo.ReadNumber(page, "pageid", 0) <= 0)
            {
                throw new WikipediaApiException("The requested article could not be found.");
            }

            var title = ReadString(page, "title");
            var extract = ReadString(page, "extract") ?? string.Empty;

            return new ArticleContent
            {
                PageId = JsonIo.ReadNumber(page, "pageid", 0),
                Title = title,
                Url = _language.ArticleUrl(title),
                ThumbnailUrl = ReadPageThumbnail(page),
                PlainText = extract,
                Summary = WikiTextParser.FirstParagraph(WikiTextParser.ExtractIntro(extract)),
                Sections = WikiTextParser.ParseSections(extract)
            };
        }

        private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await _client.GetAsync(url, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new WikipediaApiException(
                            string.Format("Wikipedia returned HTTP {0}.", (int)response.StatusCode));
                    }

                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (WikipediaApiException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new WikipediaApiException(
                    "The Wikipedia service could not be reached.", exception);
            }
        }

        private static HttpClient CreateClient()
        {
            HttpClient client;

            try
            {
                // Article payloads are large enough that compression matters
                // on a phone connection.
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };

                client = new HttpClient(handler);
            }
            catch (Exception)
            {
                // Not every profile exposes the handler, fall back to default.
                client = new HttpClient();
            }

            client.Timeout = TimeSpan.FromSeconds(30);

            try
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            }
            catch (FormatException)
            {
                // A malformed header should not stop the app from starting.
            }

            return client;
        }

        private static JsonObject ParseObject(string payload)
        {
            try
            {
                return JsonObject.Parse(payload);
            }
            catch (Exception exception)
            {
                throw new WikipediaApiException("The Wikipedia response could not be read.", exception);
            }
        }

        /// <summary>
        /// Reads a string member. GetNamedString throws on a missing key and
        /// on an explicit JSON null, so the value type is checked first.
        /// </summary>
        private static string ReadString(JsonObject source, string name)
        {
            if (source == null || !source.ContainsKey(name))
            {
                return null;
            }

            var value = source.GetNamedValue(name);

            if (value == null || value.ValueType == JsonValueType.Null)
            {
                return null;
            }

            if (value.ValueType != JsonValueType.String)
            {
                return null;
            }

            return value.GetString();
        }

        /// <summary>The search endpoint nests the image URL under "url".</summary>
        private static string ReadSearchThumbnail(JsonObject page)
        {
            var thumbnail = JsonIo.ReadObject(page, "thumbnail");

            if (thumbnail == null)
            {
                return null;
            }

            return WikiTextParser.NormalizeUrl(ReadString(thumbnail, "url"));
        }

        /// <summary>The pageimages prop nests the image URL under "source".</summary>
        private static string ReadPageThumbnail(JsonObject page)
        {
            var thumbnail = JsonIo.ReadObject(page, "thumbnail");

            if (thumbnail == null)
            {
                return null;
            }

            return WikiTextParser.NormalizeUrl(ReadString(thumbnail, "source"));
        }
    }
}
