using System;
using System.Threading.Tasks;
using Windows.UI.StartScreen;

namespace Wikipedia.Services
{
    /// <summary>Pins an article to the Start screen.</summary>
    public interface ISecondaryTileService
    {
        Task<bool> PinArticleAsync(string title, string url);
        Task<bool> IsPinnedAsync(string title);
    }

    /// <summary>
    /// A secondary tile carries its own arguments, so launching the app from
    /// the pinned tile delivers the article title back through the activation
    /// arguments. The tile uses the app logo because composing a per article
    /// image would need an image pipeline the phone profile does not provide.
    /// </summary>
    public sealed class SecondaryTileService : ISecondaryTileService
    {
        /// <summary>Prefix that marks an activation argument as a pinned article.</summary>
        public const string ArgumentPrefix = "article:";

        /// <summary>
        /// The Square150x150 tile asset. ms-appx resolves through the
        /// resource system, so the scale-240 variant on disk is picked
        /// automatically.
        /// </summary>
        private const string LogoUri = "ms-appx:///Assets/Logo.png";

        public async Task<bool> PinArticleAsync(string title, string url)
        {
            if (string.IsNullOrEmpty(title))
            {
                return false;
            }

            try
            {
                var tileId = BuildTileId(title);

                if (SecondaryTile.Exists(tileId))
                {
                    // Already pinned, nothing to do.
                    return true;
                }

                var displayName = title.Length > 40 ? title.Substring(0, 40) : title;

                var tile = new SecondaryTile(
                    tileId,
                    displayName,
                    ArgumentPrefix + title,
                    new Uri(LogoUri, UriKind.Absolute),
                    TileSize.Square150x150);

                tile.VisualElements.ShowNameOnSquare150x150Logo = true;
                tile.VisualElements.ForegroundText = ForegroundText.Light;

                return await tile.RequestCreateAsync();
            }
            catch (Exception)
            {
                // Pinning is unavailable on some configurations, the caller
                // reports it as a failed action rather than a crash.
                return false;
            }
        }

        public Task<bool> IsPinnedAsync(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(SecondaryTile.Exists(BuildTileId(title)));
        }

        /// <summary>
        /// Tile identifiers are limited in length and have to be stable across
        /// launches, so the title is normalized rather than hashed.
        /// </summary>
        private static string BuildTileId(string title)
        {
            var builder = new System.Text.StringBuilder("wp-art-");
            var appended = 0;

            foreach (var character in title)
            {
                if (appended >= 30)
                {
                    break;
                }

                builder.Append(char.IsLetterOrDigit(character) ? character : '-');
                appended++;
            }

            return builder.ToString();
        }
    }
}
