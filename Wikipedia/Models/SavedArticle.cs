using System;

namespace Wikipedia.Models
{
    /// <summary>
    /// An article the user saved for offline reading. The full plain text is
    /// kept on disk so a saved article opens without a network connection.
    /// </summary>
    public sealed class SavedArticle
    {
        public string Title { get; set; }

        public string ThumbnailUrl { get; set; }

        /// <summary>Full article text, cached so the entry works offline.</summary>
        public string PlainText { get; set; }

        public DateTimeOffset SavedAt { get; set; }
    }
}
