using System;
using Windows.Networking.Connectivity;

namespace Wikipedia.Services
{
    /// <summary>Reports whether the device currently has internet access.</summary>
    public interface IConnectivity
    {
        bool IsInternetAvailable();
    }

    public sealed class ConnectivityService : IConnectivity
    {
        /// <summary>
        /// GetInternetConnectionProfile returns null when offline, and the
        /// connectivity level can be anything below InternetAccess on a
        /// captive portal, so both are checked before reporting success.
        /// </summary>
        public bool IsInternetAvailable()
        {
            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();

                if (profile == null)
                {
                    return false;
                }

                return profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
            }
            catch (Exception)
            {
                // Treat an unexpected connectivity failure as offline so the
                // caller serves cached content instead of crashing.
                return false;
            }
        }
    }
}
