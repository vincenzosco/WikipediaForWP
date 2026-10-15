using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Wikipedia.Models;

namespace Wikipedia.Services
{
    /// <summary>Articles the user kept for offline reading.</summary>
    public interface ISavedArticleStore
    {
        Task<IReadOnlyList<SavedArticle>> GetAllAsync(CancellationToken cancellationToken);
        Task<bool> IsSavedAsync(string title, CancellationToken cancellationToken);
        Task SaveAsync(SavedArticle article, CancellationToken cancellationToken);
        Task RemoveAsync(string title, CancellationToken cancellationToken);
    }

    /// <summary>
    /// One file per saved article under <c>saved</c> in the local app folder,
    /// inside a folder per edition: the same title can be a different article
    /// in every language, so the entries must not be shared between them.
    /// The full plain text is kept so a saved article opens with no network
    /// connection. Unlike the cache, this store is never pruned automatically:
    /// only the user removes entries.
    /// </summary>
    public sealed class SavedArticleStore : ISavedArticleStore
    {
        private const string FolderName = "saved";
        private const string Extension = ".json";

        private readonly ILanguageService _language;

        private string _folderLanguage;
        private StorageFolder _folder;

        public SavedArticleStore(ILanguageService language)
        {
            if (language == null)
            {
                throw new ArgumentNullException("language");
            }

            _language = language;
        }

        public async Task<IReadOnlyList<SavedArticle>> GetAllAsync(CancellationToken cancellationToken)
        {
            var articles = new List<SavedArticle>();

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var folder = await GetFolderAsync();
                var files = await folder.GetFilesAsync();

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var article = await ReadAsync(file);

                    if (article != null)
                    {
                        articles.Add(article);
                    }
                }

                articles.Sort((left, right) => right.SavedAt.CompareTo(left.SavedAt));
            }
            catch (Exception)
            {
                // An unreadable store is reported as empty.
            }

            return articles;
        }

        public async Task<bool> IsSavedAsync(string title, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(title))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var folder = await GetFolderAsync();
                var file = await folder.TryGetFileAsync(StorageNaming.FileNameFor(title, Extension));

                return file != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task SaveAsync(SavedArticle article, CancellationToken cancellationToken)
        {
            if (article == null || string.IsNullOrEmpty(article.Title))
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var folder = await GetFolderAsync();
            var name = StorageNaming.FileNameFor(article.Title, Extension);
            var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);

            var root = new JsonObject();
            JsonIo.SetString(root, "title", article.Title);
            JsonIo.SetString(root, "thumbnailUrl", article.ThumbnailUrl);
            JsonIo.SetString(root, "plainText", article.PlainText);
            JsonIo.SetString(root, "savedAt", JsonIo.FormatDate(article.SavedAt));

            await FileIO.WriteTextAsync(file, root.Stringify());
        }

        public async Task RemoveAsync(string title, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(title))
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var folder = await GetFolderAsync();
                var file = await folder.TryGetFileAsync(StorageNaming.FileNameFor(title, Extension));

                if (file != null)
                {
                    await file.DeleteAsync();
                }
            }
            catch (Exception)
            {
                // Removing something that is already gone is not an error.
            }
        }

        private static async Task<SavedArticle> ReadAsync(StorageFile file)
        {
            try
            {
                var text = await FileIO.ReadTextAsync(file);
                var root = JsonIo.Parse(text);

                if (root == null)
                {
                    return null;
                }

                var title = JsonIo.ReadString(root, "title");

                if (string.IsNullOrEmpty(title))
                {
                    return null;
                }

                return new SavedArticle
                {
                    Title = title,
                    ThumbnailUrl = JsonIo.ReadString(root, "thumbnailUrl"),
                    PlainText = JsonIo.ReadString(root, "plainText") ?? string.Empty,
                    SavedAt = JsonIo.ReadDate(root, "savedAt", DateTimeOffset.MinValue)
                };
            }
            catch (Exception)
            {
                return null;
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
