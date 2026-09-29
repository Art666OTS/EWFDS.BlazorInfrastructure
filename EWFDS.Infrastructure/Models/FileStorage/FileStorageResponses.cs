namespace EWFDS.Infrastructure.Models.FileStorage;

/// <summary>
/// Response model for file upload operations.
/// </summary>
public class FileUploadResponse
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response model for general file operations (download, delete).
/// </summary>
public class FileOperationResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response model for file list operations.
/// </summary>
public class FileListResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<FileListItem> Files { get; set; } = [];
}

/// <summary>
/// Represents a file in a list response.
/// </summary>
public class FileListItem
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string ContentType { get; set; } = string.Empty;
}
