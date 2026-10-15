using System.Text;

namespace Wikipedia.Services
{
    /// <summary>
    /// Builds stable file names for storage keyed by article title. Titles
    /// contain characters that are not valid in a Windows file name, so those
    /// are replaced, and a short hash of the original title is appended so two
    /// different titles cannot collide after sanitizing.
    /// </summary>
    internal static class StorageNaming
    {
        private const int MaxPrefixLength = 60;

        internal static string FileNameFor(string title, string extension)
        {
            var safe = Sanitize(title ?? string.Empty);
            var hash = StableHash(title ?? string.Empty);

            return string.Format("{0}-{1}{2}", safe, hash, extension);
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder(value.Length);

            foreach (var character in value)
            {
                var isSafe = char.IsLetterOrDigit(character) || character == '-' || character == '_';
                builder.Append(isSafe ? character : '_');

                if (builder.Length >= MaxPrefixLength)
                {
                    break;
                }
            }

            var trimmed = builder.ToString().Trim('_');

            return string.IsNullOrEmpty(trimmed) ? "article" : trimmed;
        }

        /// <summary>
        /// FNV-1a over the UTF-8 bytes. It only has to be deterministic across
        /// runs, so a crypto provider is not needed.
        /// </summary>
        private static string StableHash(string value)
        {
            unchecked
            {
                const uint OffsetBasis = 2166136261;
                const uint Prime = 16777619;

                var hash = OffsetBasis;

                foreach (var octet in Encoding.UTF8.GetBytes(value))
                {
                    hash ^= octet;
                    hash *= Prime;
                }

                return hash.ToString("x8");
            }
        }
    }
}
