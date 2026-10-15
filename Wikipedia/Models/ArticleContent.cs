using System.Collections.Generic;

namespace Wikipedia.Models
{
    /// <summary>
    /// A complete article as fetched from the MediaWiki action API.
    /// Plain text only: this app renders articles with native XAML controls
    /// instead of a WebView, which keeps the payload around a tenth of the
    /// size of the equivalent HTML document.
    /// </summary>
    public sealed class ArticleContent
    {
        public int PageId { get; set; }

        /// <summary>Display title, after following any redirect.</summary>
        public string Title { get; set; }

        /// <summary>Canonical web URL for "open in browser" style actions.</summary>
        public string Url { get; set; }

        /// <summary>Lead image URL, or null when the article has none.</summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>Complete article text including the intro.</summary>
        public string PlainText { get; set; }

        /// <summary>Introductory paragraph, used as a short summary.</summary>
        public string Summary { get; set; }

        /// <summary>
        /// Sections parsed out of the plain text. The intro paragraph is not
        /// included here, it is exposed through <see cref="Summary"/>.
        /// </summary>
        public IReadOnlyList<ArticleSection> Sections { get; set; }
    }
}
