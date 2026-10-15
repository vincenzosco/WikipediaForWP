using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Wikipedia.Models;

namespace Wikipedia.Services
{
    /// <summary>
    /// Short lived cache of recently opened articles, so going back to an
    /// article does not hit the network twice.
    /// </summary>
    public interface IArticleCache
    {
        Task<ArticleContent> TryGetAsync(string title, CancellationToken cancellationToken);
        Task PutAsync(ArticleContent article, CancellationToken cancellationToken);
        Task ClearAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// One file per article under <c>cache</c> in the local app folder, inside a
    /// folder per edition: the same title can be a different article in every
    /// language, so entries must never be shared between them.
    /// Sections are not stored: they are re-derived from the plain text on
    /// read, which keeps each entry roughly the size of the article text.
    /// The folder is pruned to <see cref="MaxEntries"/> by creation time, so
    /// the cache cannot grow without bound on a phone.
    /// </summary>
    public sealed class ArticleCache : IArticleCache
    {
        private const string FolderName = "cache";
        private const string Extension = ".json";
        private const int MaxEntries = 15;

        private readonly ILanguageService _language;

        private string _folderLanguage;
        private StorageFolder _folder;

        public ArticleCache(ILanguageService language)
        {
            if (language == null)
            {
                throw new ArgumentNullException("language");
            }

            _language = language;
        }

        public async Task<ArticleContent> TryGetAsync(string title, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(title))
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var folder = await GetFolderAsync();
                var file = await folder.TryGetFileAsync(StorageNaming.FileNameFor(title, Extension));

                if (file == null)
                {
                    return null;
                }

                var text = await FileIO.ReadTextAsync(file);
                var root = JsonIo.Parse(text);

                if (root == null)
                {
                    return null;
                }

                var plainText = JsonIo.ReadString(root, "plainText") ?? string.Empty;

                return new ArticleContent
                {
                    PageId = JsonIo.ReadNumber(root, "pageId", 0),
                    Title = JsonIo.ReadString(root, "title") ?? title,
                    Url = JsonIo.ReadString(root, "url"),
                    ThumbnailUrl = JsonIo.ReadString(root, "thumbnailUrl"),
                    PlainText = plainText,
                    Summary = WikiTextParser.FirstParagraph(WikiTextParser.ExtractIntro(plainText)),
                    Sections = WikiTextParser.ParseSections(plainText)
                };
            }
            catch (Exception)
            {
                // A cache that cannot be read is simply a cache miss.
                return null;
            }
        }

        public async Task PutAsync(ArticleContent article, CancellationToken cancellationToken)
        {
            if (article == null || string.IsNullOrEmpty(article.Title))
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var folder = await GetFolderAsync();
                var name = StorageNaming.FileNameFor(article.Title, Extension);
                var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);

                var root = new JsonObject();
                JsonIo.SetString(root, "title", article.Title);
                JsonIo.SetNumber(root, "pageId", article.PageId);
                JsonIo.SetString(root, "url", article.Url);
                JsonIo.SetString(root, "thumbnailUrl", article.ThumbnailUrl);
                JsonIo.SetString(root, "plainText", article.PlainText);
                JsonIo.SetString(root, "cachedAt", JsonIo.FormatDate(DateTimeOffset.Now));

                await FileIO.WriteTextAsync(file, root.Stringify());

                await PruneAsync(folder);
            }
            catch (Exception)
            {
                // Caching is best effort and never blocks reading an article.
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Every edition is cleared rather than only the one being read,
                // so the button means what it says. Dropping the per language
                // folders also invalidates the handle cached below.
                var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    FolderName, CreationCollisionOption.OpenIfExists);

                foreach (var folder in await root.GetFoldersAsync())
                {
                    try
                    {
                        await folder.DeleteAsync();
                    }
                    catch (Exception)
                    {
                        // Leave the remaining folders for the next attempt.
                    }
                }

                _folder = null;
                _folderLanguage = null;
            }
            catch (Exception)
            {
                // A cache that cannot be cleared is not a user visible failure.
            }
        }

        private static async Task PruneAsync(StorageFolder folder)
        {
            var files = await folder.GetFilesAsync();

            if (files.Count <= MaxEntries)
            {
                return;
            }

            var ordered = new List<StorageFile>(files);
            ordered.Sort((left, right) => left.DateCreated.CompareTo(right.DateCreated));

            var excess = ordered.Count - MaxEntries;

            for (var index = 0; index < excess; index++)
            {
                try
                {
                    await ordered[index].DeleteAsync();
                }
                catch (Exception)
                {
                    // Another entry can be pruned on the next write.
                }
            }
        }

        private async Task<StorageFolder> GetFolderAsync()
        {
            var language = _language.WikiLanguage;

            if (_folder == null || !string.Equals(_folderLanguage, language, StringComparison.OrdinalIgnoreCase))
            {
                var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    FolderName, CreationCollisionOption.OpenIfExists);

                _folder = await root.CreateFolderAsync(language, CreationCollisionOption.OpenIfExists);
                _folderLanguage = language;
            }

            return _folder;
        }
    }
}
