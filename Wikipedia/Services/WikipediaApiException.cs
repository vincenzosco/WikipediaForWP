using System;

namespace Wikipedia.Services
{
    /// <summary>
    /// Raised when the Wikipedia API cannot satisfy a request, either because
    /// of a transport failure or an unsuccessful status code. Callers map this
    /// to a short user facing message rather than surfacing the raw text.
    /// </summary>
    public sealed class WikipediaApiException : Exception
    {
        public WikipediaApiException(string message)
            : base(message)
        {
        }

        public WikipediaApiException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
