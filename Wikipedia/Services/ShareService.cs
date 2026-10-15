using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Wikipedia.Services
{
    /// <summary>Hands an article to the system share contract.</summary>
    public interface IShareService
    {
        void ShareArticle(string title, string url);
    }

    /// <summary>
    /// The share contract only exists while a handler is attached, and the
    /// handler must be detached again or it accumulates across every share.
    /// The delegate is therefore removed inside its own body before the
    /// request is populated.
    /// </summary>
    public sealed class ShareService : IShareService
    {
        private readonly Localizer _localizer;

        public ShareService(Localizer localizer)
        {
            _localizer = localizer;
        }

        public void ShareArticle(string title, string url)
        {
            try
            {
                var manager = DataTransferManager.GetForCurrentView();

                TypedEventHandler<DataTransferManager, DataRequestedEventArgs> handler = null;

                handler = (sender, args) =>
                {
                    manager.DataRequested -= handler;

                    var request = args.Request;
                    var data = request.Data;

                    data.Properties.Title = title ?? string.Empty;
                    data.Properties.Description = _localizer.Get("Share_Description");

                    if (!string.IsNullOrEmpty(url))
                    {
                        data.SetWebLink(new Uri(url));
                        data.SetText(url);
                    }
                    else if (!string.IsNullOrEmpty(title))
                    {
                        data.SetText(title);
                    }
                };

                manager.DataRequested += handler;

                DataTransferManager.ShowShareUI();
            }
            catch (Exception)
            {
                // Sharing is optional and must never take the page down.
            }
        }
    }
}
