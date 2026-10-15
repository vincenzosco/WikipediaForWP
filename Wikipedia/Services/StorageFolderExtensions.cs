using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace Wikipedia.Services
{
    /// <summary>
    /// Windows Phone 8.1 has no <c>StorageFolder.TryGetItemAsync</c>, so asking
    /// whether a file exists has to be done by attempting the read and
    /// absorbing only the not-found failure.
    /// </summary>
    internal static class StorageFolderExtensions
    {
        /// <summary>
        /// Returns the named file, or <c>null</c> when it is not there. A
        /// permission or IO failure still surfaces, because the caller has to
        /// see that.
        /// </summary>
        /// <param name="folder">Folder to look in.</param>
        /// <param name="name">File name, not a path.</param>
        /// <returns>The file, or <c>null</c>.</returns>
        public static async Task<StorageFile> TryGetFileAsync(this StorageFolder folder, string name)
        {
            if (folder == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            try
            {
                return await folder.GetFileAsync(name);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
