using System;

namespace Wikipedia.Models
{
    /// <summary>
    /// A Wikipedia edition the user can read and search. The code is the
    /// subdomain used to build every request URL, and it is not always the
    /// BCP 47 tag: Simple English is <c>simple</c> and Cantonese is
    /// <c>zh-yue</c>, so the tag can never be substituted for it.
    /// </summary>
    public sealed class WikiLanguage
    {
        public WikiLanguage(string code, string name, bool isRightToLeft)
        {
            Code = code;
            Name = name;
            IsRightToLeft = isRightToLeft;
        }

        /// <summary>Edition code, used to build the API and article URLs.</summary>
        public string Code { get; private set; }

        /// <summary>
        /// The name of the edition written in its own language, which is what
        /// the picker shows: a reader looking for Japanese looks for 日本語.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>True for the editions written right to left.</summary>
        public bool IsRightToLeft { get; private set; }

        public override string ToString()
        {
            return Name + " (" + Code + ")";
        }
    }
}
