using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Wikipedia.Models;

namespace Wikipedia.Services
{
    /// <summary>
    /// Parses the plain text extract returned by
    /// <c>prop=extracts&amp;explaintext=1&amp;exsectionformat=wiki</c>.
    /// That format marks headings with leading and trailing equals signs, for
    /// example <c>== Storia ==</c> for a top level section and
    /// <c>=== Origini ===</c> for a subsection.
    /// </summary>
    public static class WikiTextParser
    {
        // RegexOptions.Compiled is deliberately not used: the .NET Core
        // profile used by Windows Store apps has no runtime code generation
        // and the option is not supported.
        private static readonly Regex HeadingPattern =
            new Regex(@"^(={2,6})\s*(.+?)\s*\1\s*$", RegexOptions.None);

        private static readonly Regex TagPattern =
            new Regex("<[^>]+>", RegexOptions.None);

        /// <summary>
        /// Splits an extract into sections. The introductory text that appears
        /// before the first heading is not returned here, use
        /// <see cref="ExtractIntro"/> for that.
        /// </summary>
        public static IReadOnlyList<ArticleSection> ParseSections(string extract)
        {
            var sections = new List<ArticleSection>();

            if (string.IsNullOrEmpty(extract))
            {
                return sections;
            }

            var lines = Normalize(extract).Split('\n');
            ArticleSection current = null;
            var body = new StringBuilder();

            foreach (var line in lines)
            {
                var match = HeadingPattern.Match(line);

                if (match.Success)
                {
                    if (current != null)
                    {
                        current.Body = body.ToString().Trim();
                        sections.Add(current);
                    }

                    current = new ArticleSection
                    {
                        // "==" is a top level section, so depth starts at 1.
                        Level = match.Groups[1].Value.Length - 1,
                        Heading = match.Groups[2].Value,
                        Body = string.Empty
                    };

                    body.Length = 0;
                    continue;
                }

                if (current != null)
                {
                    body.AppendLine(line);
                }
            }

            if (current != null)
            {
                current.Body = body.ToString().Trim();
                sections.Add(current);
            }

            return sections;
        }

        /// <summary>Text before the first heading.</summary>
        public static string ExtractIntro(string extract)
        {
            if (string.IsNullOrEmpty(extract))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            foreach (var line in Normalize(extract).Split('\n'))
            {
                if (HeadingPattern.IsMatch(line))
                {
                    break;
                }

                builder.AppendLine(line);
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// First paragraph of a block of text, used as a one line summary
        /// where the API does not provide a description.
        /// </summary>
        public static string FirstParagraph(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var normalized = Normalize(text);
            var separator = normalized.IndexOf("\n\n", StringComparison.Ordinal);

            var paragraph = separator > 0
                ? normalized.Substring(0, separator)
                : normalized;

            return CollapseWhitespace(paragraph);
        }

        /// <summary>
        /// Removes markup from a search excerpt. The search endpoint returns
        /// the matched terms wrapped in span elements, which would otherwise
        /// show up as literal angle brackets in the list.
        /// </summary>
        public static string StripHtml(string markup)
        {
            if (string.IsNullOrEmpty(markup))
            {
                return string.Empty;
            }

            var withoutTags = TagPattern.Replace(markup, string.Empty);
            return DecodeEntities(withoutTags);
        }

        /// <summary>
        /// Search results return protocol relative thumbnail URLs such as
        /// "//thumb.wikimedia.org/...". Image sources need an absolute URI.
        /// </summary>
        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                return "https:" + url;
            }

            return url;
        }

        /// <summary>Trims and collapses runs of whitespace into single spaces.</summary>
        public static string CollapseWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            var previousWasSpace = false;

            foreach (var character in text)
            {
                var isSpace = char.IsWhiteSpace(character);

                if (isSpace)
                {
                    if (!previousWasSpace && builder.Length > 0)
                    {
                        builder.Append(' ');
                    }
                }
                else
                {
                    builder.Append(character);
                }

                previousWasSpace = isSpace;
            }

            return builder.ToString().Trim();
        }

        private static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static string DecodeEntities(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var decoded = text
                .Replace("&nbsp;", " ")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&apos;", "'")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&");

            return decoded;
        }
    }
}
