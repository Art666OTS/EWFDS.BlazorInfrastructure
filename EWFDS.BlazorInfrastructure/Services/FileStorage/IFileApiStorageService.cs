namespace EWFDS.BlazorInfrastructure.Services.FileStorage;

/// <summary>
/// Result of a file storage operation.
/// </summary>
public class FileStorageResult
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Information about a file stored via File API.
/// </summary>
public class FileApiFileInfo
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
}

/// <summary>
/// Service interface for file storage operations via external File API.
/// Implementations include Azure Blob Storage and external File API.
/// </summary>
public interface IFileApiStorageService
{
    /// <summary>
    /// Checks if the storage service is healthy and reachable.
    /// </summary>
    Task<(bool IsHealthy, string Message)> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file to storage.
    /// </summary>
    Task<FileStorageResult> UploadAsync(string virtualDir, string folder, string fileName, Stream fileStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file to storage from a byte array.
    /// Use this overload when the stream cannot be read directly (e.g., Telerik FileSelect in Blazor Server).
    /// </summary>
    Task<FileStorageResult> UploadAsync(string virtualDir, string folder, string fileName, byte[] fileBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files in a folder.
    /// </summary>
    Task<List<FileApiFileInfo>> ListFilesAsync(string virtualDir, string folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    Task<FileStorageResult> DeleteAsync(string virtualDir, string folder, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from storage and returns its content.
    /// </summary>
    Task<(Stream? Stream, string ContentType, string? ErrorMessage)> DownloadAsync(string virtualDir, string folder, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the URL for downloading a file.
    /// </summary>
    string GetDownloadUrl(string virtualDir, string folder, string fileName);
}
