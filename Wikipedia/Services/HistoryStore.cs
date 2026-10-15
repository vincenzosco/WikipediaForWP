using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Wikipedia.Models;

namespace Wikipedia.Services
{
    /// <summary>Articles the user opened, most recent first.</summary>
    public interface IHistoryStore
    {
        Task<IReadOnlyList<HistoryEntry>> GetAllAsync(CancellationToken cancellationToken);
        Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken);
        Task ClearAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// A single small JSON document per edition under <c>history-CODE.json</c>.
    /// Only titles, thumbnails and timestamps are stored, so the whole list
    /// stays in the low kilobytes even at the maximum length, and the article
    /// body is re-fetched from the cache or the network when opened.
    /// </summary>
    public sealed class HistoryStore : IHistoryStore
    {
        /// <summary>
        /// One document per edition: the same title is a different article in
        /// every language, so the lists cannot be shared.
        /// </summary>
        private const string FileNameFormat = "history-{0}.json";

        /// <summary>Prefix shared by every per edition document.</summary>
        private const string FileNamePrefix = "history-";

        private const string EntriesKey = "entries";

        /// <summary>Keeps the document small and the list useful.</summary>
        private const int MaxEntries = 60;

        private readonly ILanguageService _language;

        public HistoryStore(ILanguageService language)
        {
            if (language == null)
            {
                throw new ArgumentNullException("language");
            }

            _language = language;
        }

        public async Task<IReadOnlyList<HistoryEntry>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entries = await ReadAllAsync();

            entries.Sort((left, right) => right.ViewedAt.CompareTo(left.ViewedAt));

            return entries;
        }

        public async Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Title))
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var entries = await ReadAllAsync();

            // Re-opening an article moves it to the top instead of duplicating.
            entries.RemoveAll(existing => string.Equals(existing.Title, entry.Title, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, entry);

            while (entries.Count > MaxEntries)
            {
                entries.RemoveAt(entries.Count - 1);
            }

            await WriteAllAsync(entries);
        }

        public async Task ClearAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Clearing the history means clearing it, not just the edition
                // being read, so every per language document goes.
                var files = await ApplicationData.Current.LocalFolder.GetFilesAsync();

                foreach (var file in files)
                {
                    if (!file.Name.StartsWith(FileNamePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        await file.DeleteAsync();
                    }
                    catch (Exception)
                    {
                        // Leave the remaining documents for the next attempt.
                    }
                }
            }
            catch (Exception)
            {
                // Clearing an absent history is not an error.
            }
        }

        private async Task<List<HistoryEntry>> ReadAllAsync()
        {
            var entries = new List<HistoryEntry>();

            try
            {
                var file = await GetFileAsync();

                if (file == null)
                {
                    return entries;
                }

                var root = JsonIo.Parse(await FileIO.ReadTextAsync(file));

                if (root == null)
                {
                    return entries;
                }

                var array = JsonIo.ReadArray(root, EntriesKey);

                if (array == null)
                {
                    return entries;
                }

                foreach (var item in array)
                {
                    if (item.ValueType != JsonValueType.Object)
                    {
                        continue;
                    }

                    var entryObject = item.GetObject();
                    var title = JsonIo.ReadString(entryObject, "title");

                    if (string.IsNullOrEmpty(title))
                    {
                        continue;
                    }

                    entries.Add(new HistoryEntry
                    {
                        Title = title,
                        ThumbnailUrl = JsonIo.ReadString(entryObject, "thumbnailUrl"),
                        ViewedAt = JsonIo.ReadDate(entryObject, "viewedAt", DateTimeOffset.MinValue)
                    });
                }
            }
            catch (Exception)
            {
                // A corrupt history file reads as empty rather than crashing.
            }

            return entries;
        }

        private async Task WriteAllAsync(List<HistoryEntry> entries)
        {
            var array = new JsonArray();

            foreach (var entry in entries)
            {
                var item = new JsonObject();
                JsonIo.SetString(item, "title", entry.Title);
                JsonIo.SetString(item, "thumbnailUrl", entry.ThumbnailUrl);
                JsonIo.SetString(item, "viewedAt", JsonIo.FormatDate(entry.ViewedAt));

                array.Add(item);
            }

            var root = new JsonObject();
            root.SetNamedValue(EntriesKey, array);

            var name = string.Format(FileNameFormat, _language.WikiLanguage);

            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                name, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, root.Stringify());
        }

        private Task<StorageFile> GetFileAsync()
        {
            var name = string.Format(FileNameFormat, _language.WikiLanguage);

            return ApplicationData.Current.LocalFolder.TryGetFileAsync(name);
        }
    }
}
