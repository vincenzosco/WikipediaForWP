namespace Wikipedia.Models
{
    /// <summary>
    /// A single entry returned by the Wikipedia search endpoint.
    /// </summary>
    public sealed class SearchResult
    {
        /// <summary>Numeric page identifier.</summary>
        public int PageId { get; set; }

        /// <summary>Page title, also used as the article identifier.</summary>
        public string Title { get; set; }

        /// <summary>Short description supplied by the API, or null when absent.</summary>
        public string Description { get; set; }

        /// <summary>Search excerpt with all HTML markup already removed.</summary>
        public string Excerpt { get; set; }

        /// <summary>Absolute thumbnail URL, or null when the page has no image.</summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>Canonical article URL, or null when no URL was returned.</summary>
        public string Url { get; set; }
    }
}
