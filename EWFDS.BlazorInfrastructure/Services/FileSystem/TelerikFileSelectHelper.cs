using Telerik.Blazor.Components;

namespace EWFDS.BlazorInfrastructure.Services.FileSystem
{
    /// <summary>
    /// Helper for reading files from Telerik FileSelect component.
    /// Centralizes the stream-reading pattern that must occur during OnSelect
    /// (stream is only valid in that event context due to Blazor Server/SignalR).
    /// </summary>
    public static class TelerikFileSelectHelper
    {
        /// <summary>
        /// Reads bytes from a Telerik FileSelectFileInfo during OnSelect.
        /// Must be called during the OnSelect event - stream is only valid in that context.
        /// </summary>
        /// <param name="file">The file info from FileSelectEventArgs.Files</param>
        /// <returns>Tuple of (bytes, errorMessage). Bytes is null if error occurred.</returns>
        public static async Task<(byte[]? Bytes, string? ErrorMessage)> ReadFileBytesAsync(FileSelectFileInfo file)
        {
            // Validate file first
            if (file.InvalidExtension)
            {
                return (null, $"Invalid file extension: {file.Name}");
            }

            if (file.InvalidMaxFileSize)
            {
                return (null, $"File too large: {file.Name}");
            }

            if (file.InvalidMinFileSize)
            {
                return (null, $"File too small: {file.Name}");
            }

            try
            {
                using var ms = new MemoryStream();
                await file.Stream.CopyToAsync(ms);
                return (ms.ToArray(), null);
            }
            catch (Exception ex)
            {
                return (null, $"Error reading {file.Name}: {ex.Message}");
            }
        }
    }
}
