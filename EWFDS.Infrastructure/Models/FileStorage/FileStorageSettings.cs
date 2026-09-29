namespace EWFDS.Infrastructure.Models.FileStorage;

/// <summary>
/// Configuration settings for file storage.
/// </summary>
public class FileStorageSettings
{
    /// <summary>
    /// Maximum file size in bytes. Default is 50MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
}
