namespace Wikipedia.Models
{
    /// <summary>
    /// One heading of an article together with the body text that follows it.
    /// Sections are produced by parsing the wiki formatted plain text extract.
    /// </summary>
    public sealed class ArticleSection
    {
        /// <summary>Heading depth. A value of 1 is a top level section, 2 a subsection.</summary>
        public int Level { get; set; }

        /// <summary>Heading text with the surrounding markup removed.</summary>
        public string Heading { get; set; }

        /// <summary>Body text belonging to this heading. Never null, may be empty.</summary>
        public string Body { get; set; }
    }
}
