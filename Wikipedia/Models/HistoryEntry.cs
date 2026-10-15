using System;

namespace Wikipedia.Models
{
    /// <summary>
    /// One entry in the reading history. Only enough data to render the list
    /// is kept, the article itself is re-fetched on demand and comes from the
    /// offline cache whenever it is still there.
    /// </summary>
    public sealed class HistoryEntry
    {
        public string Title { get; set; }

        public string ThumbnailUrl { get; set; }

        public DateTimeOffset ViewedAt { get; set; }
    }
}
