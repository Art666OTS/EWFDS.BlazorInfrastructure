using EWFDS.Common.FileStorage;
using EWFDS.Infrastructure.Models.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EWFDS.Infrastructure.Controllers;

/// <summary>
/// File storage API for upload, download, list, and delete operations.
/// Stores files directly in configured VirtualDirectories.
/// </summary>
[Route("api/fileproxy")]
[ApiController]
[Authorize]
public class FileStorageController : ControllerBase
{
    private readonly ILogger<FileStorageController> _logger;
    private readonly FileStorageSettings _settings;
    private readonly IConfiguration _configuration;
    private readonly IFileApiStorageService _fileStorageService;

    public FileStorageController(
        ILogger<FileStorageController> logger,
        IOptions<FileStorageSettings> settings,
        IConfiguration configuration,
        IFileApiStorageService fileStorageService)
    {
        _logger = logger;
        _settings = settings.Value;
        _configuration = configuration;
        _fileStorageService = fileStorageService;
    }

    #region Upload Operations

    /// <summary>
    /// Upload a file to storage.
    /// Usage: POST /api/fileproxy/upload?virtualDir={virtualDirectory}&amp;folder={folderPath}
    /// Body: multipart/form-data with file
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(52428800)] // 50MB
    public async Task<IActionResult> UploadFile(
        [FromQuery] string virtualDir,
        [FromQuery] string folder,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(virtualDir))
        {
            virtualDir = "Documents";
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            return BadRequest(new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "Folder parameter is required"
            });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "No file provided"
            });
        }

        if (file.Length > _settings.MaxFileSizeBytes)
        {
            return BadRequest(new FileUploadResponse
            {
                Success = false,
                ErrorMessage = $"File exceeds maximum size of {_settings.MaxFileSizeBytes / 1024 / 1024}MB"
            });
        }

        try
        {
            var basePath = GetStoragePath(virtualDir);

            if (string.IsNullOrEmpty(basePath))
            {
                _logger.LogError("Virtual directory '{VirtualDir}' not configured", virtualDir);
                return BadRequest(new FileUploadResponse
                {
                    Success = false,
                    ErrorMessage = $"Virtual directory '{virtualDir}' not configured"
                });
            }

            var sanitizedFolder = SanitizePath(folder);
            var fullFolderPath = Path.Combine(basePath, sanitizedFolder);

            // Validate path stays within base directory
            if (!IsPathWithinBase(basePath, fullFolderPath))
            {
                _logger.LogWarning("Path traversal attempt detected: {Folder}", folder);
                return BadRequest(new FileUploadResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid folder path"
                });
            }

            Directory.CreateDirectory(fullFolderPath);

            var fileName = SanitizeFileName(file.FileName);
            var fullFilePath = Path.Combine(fullFolderPath, fileName);

            _logger.LogInformation("Uploading file {FileName} to {Path}", fileName, fullFilePath);

            using (var stream = new FileStream(fullFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            _logger.LogInformation("Successfully saved file {FileName} to {Path}", fileName, fullFilePath);

            return Ok(new FileUploadResponse
            {
                Success = true,
                FileName = fileName,
                FilePath = $"{virtualDir}/{folder}/{fileName}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
            return StatusCode(500, new FileUploadResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while uploading the file"
            });
        }
    }

    #endregion Upload Operations

    #region Download Operations

    /// <summary>
    /// Download a file from storage via FileAPI proxy.
    /// Usage: GET /api/fileproxy/download?virtualDir={virtualDir}&amp;folder={folderPath}&amp;file={fileName}
    /// </summary>
    [HttpGet("download")]
    public async Task<IActionResult> DownloadFile(
        [FromQuery] string? virtualDir = "Documents",
        [FromQuery] string? folder = "",
        [FromQuery] string? file = "")
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = "File parameter is required" });
        }

        try
        {
            _logger.LogInformation("Proxying download request for {VirtualDir}/{Folder}/{File}", virtualDir, folder, file);

            var (stream, contentType, errorMessage) = await _fileStorageService.DownloadAsync(
                virtualDir ?? "Documents",
                folder ?? "",
                file);

            if (stream == null)
            {
                _logger.LogWarning("Download failed: {Error}", errorMessage);
                return NotFound(new FileOperationResponse { Success = false, ErrorMessage = errorMessage ?? "File not found" });
            }

            _logger.LogInformation("Successfully proxied file {File}", file);

            // Use inline disposition for viewable file types (PDF, images, text)
            // so they display in iframe/browser instead of downloading
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (CanDisplayInline(extension))
            {
                // Override content-type based on extension to ensure correct MIME type
                // (Azurite/blob storage may return application/octet-stream)
                var correctContentType = GetContentTypeFromExtension(extension);
                Response.Headers.ContentDisposition = $"inline; filename=\"{file}\"";
                return File(stream, correctContentType);
            }

            // For other file types, trigger download
            return File(stream, contentType, file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying download for {File}", file);
            return StatusCode(500, new FileOperationResponse { Success = false, ErrorMessage = "An error occurred while downloading the file" });
        }
    }

    /// <summary>
    /// Download a file directly from the local virtual directory storage.
    /// Usage: GET /api/fileproxy/local?virtualDir={virtualDir}&amp;folder={folderPath}&amp;file={fileName}
    /// </summary>
    [HttpGet("local")]
    public IActionResult DownloadLocalFile(
        [FromQuery] string? virtualDir = "Documents",
        [FromQuery] string? folder = "",
        [FromQuery] string? file = "")
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = "File parameter is required" });
        }

        var basePath = GetStoragePath(virtualDir ?? "Documents");
        if (basePath == null)
        {
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = $"Unknown virtual directory: {virtualDir}" });
        }

        var fullPath = string.IsNullOrEmpty(folder)
            ? Path.Combine(basePath, SanitizePath(file))
            : Path.Combine(basePath, SanitizePath(folder), SanitizePath(file));

        // Validate path stays within base directory
        if (!IsPathWithinBase(basePath, fullPath))
        {
            _logger.LogWarning("Path traversal attempt detected in download: {Folder}/{File}", folder, file);
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = "Invalid path" });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            _logger.LogWarning("File not found: {Path}", fullPath);
            return NotFound(new FileOperationResponse { Success = false, ErrorMessage = "File not found" });
        }

        try
        {
            var fileName = Path.GetFileName(fullPath);
            var contentType = GetContentTypeFromExtension(Path.GetExtension(fullPath));
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (CanDisplayInline(extension))
            {
                Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
                return File(stream, contentType);
            }

            // For other file types, trigger download
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {VirtualDir}/{FilePath}", virtualDir, fullPath);
            return StatusCode(500, new FileOperationResponse { Success = false, ErrorMessage = "An error occurred while downloading the file" });
        }
    }

    /// <summary>
    /// Checks if the file type can be displayed inline in browser/iframe (PDF and images only)
    /// </summary>
    private static bool CanDisplayInline(string extension)
    {
        return extension is ".pdf" or ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
    }

    /// <summary>
    /// Generic file endpoint using full relative path.
    /// Usage: GET /api/fileproxy/file?path={relativePath}
    /// </summary>
    [HttpGet("file")]
    public IActionResult GetFile([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = "Path parameter is required" });
        }

        // Parse path: first segment is virtualDir, rest is folder/file
        var segments = path.Trim('/').Split('/', 2);
        var virtualDir = segments.Length > 0 ? segments[0] : "Documents";
        var relativePath = segments.Length > 1 ? segments[1] : "";

        if (string.IsNullOrEmpty(relativePath))
        {
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = "Invalid path format" });
        }

        var basePath = GetStoragePath(virtualDir);
        if (basePath == null)
        {
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = $"Unknown virtual directory: {virtualDir}" });
        }

        var fullPath = Path.Combine(basePath, SanitizePath(relativePath));

        // Validate path stays within base directory
        if (!IsPathWithinBase(basePath, fullPath))
        {
            _logger.LogWarning("Path traversal attempt detected: {Path}", path);
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = "Invalid path" });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            _logger.LogWarning("File not found: {Path}", fullPath);
            return NotFound(new FileOperationResponse { Success = false, ErrorMessage = "File not found" });
        }

        try
        {
            var contentType = GetContentTypeFromExtension(Path.GetExtension(fullPath));
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file {Path}", fullPath);
            return StatusCode(500, new FileOperationResponse { Success = false, ErrorMessage = "An error occurred while reading the file" });
        }
    }

    #endregion Download Operations

    #region List Operations

    /// <summary>
    /// List files in a folder.
    /// Usage: GET /api/fileproxy/list?virtualDir={virtualDir}&amp;folder={folderPath}
    /// </summary>
    [HttpGet("list")]
    public IActionResult ListFiles(
        [FromQuery] string? virtualDir = "Documents",
        [FromQuery] string? folder = "")
    {
        try
        {
            var basePath = GetStoragePath(virtualDir ?? "Documents");
            if (basePath == null)
            {
                return BadRequest(new FileListResponse { Success = false, ErrorMessage = $"Unknown virtual directory: {virtualDir}" });
            }

            var fullPath = string.IsNullOrEmpty(folder)
                ? basePath
                : Path.Combine(basePath, SanitizePath(folder));

            // Validate path stays within base directory
            if (!IsPathWithinBase(basePath, fullPath))
            {
                _logger.LogWarning("Path traversal attempt detected in list: {Folder}", folder);
                return BadRequest(new FileListResponse { Success = false, ErrorMessage = "Invalid folder path" });
            }

            if (!Directory.Exists(fullPath))
            {
                _logger.LogInformation("Directory does not exist: {Path}", fullPath);
                return Ok(new FileListResponse { Success = true });
            }

            var files = Directory.GetFiles(fullPath)
                .Select(f => new FileInfo(f))
                .Select(fi => new FileListItem
                {
                    Name = fi.Name,
                    Size = fi.Length,
                    LastModified = fi.LastWriteTimeUtc,
                    ContentType = GetContentTypeFromExtension(fi.Extension)
                })
                .OrderByDescending(f => f.LastModified)
                .ToList();

            _logger.LogInformation("Listed {Count} files in {Path}", files.Count, fullPath);

            return Ok(new FileListResponse { Success = true, Files = files });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from {VirtualDir}/{Folder}", virtualDir, folder);
            return StatusCode(500, new FileListResponse { Success = false, ErrorMessage = "An error occurred while listing files" });
        }
    }

    #endregion List Operations

    #region Delete Operations

    /// <summary>
    /// Delete a file from storage.
    /// Usage: DELETE /api/fileproxy/delete?virtualDir={virtualDir}&amp;folder={folderPath}&amp;file={fileName}
    /// </summary>
    [HttpDelete("delete")]
    public IActionResult DeleteFile(
        [FromQuery] string? virtualDir = "Documents",
        [FromQuery] string? folder = "",
        [FromQuery] string? file = "")
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = "File parameter is required" });
        }

        try
        {
            var basePath = GetStoragePath(virtualDir ?? "Documents");
            if (basePath == null)
            {
                return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = $"Unknown virtual directory: {virtualDir}" });
            }

            var fullPath = string.IsNullOrEmpty(folder)
                ? Path.Combine(basePath, SanitizePath(file))
                : Path.Combine(basePath, SanitizePath(folder), SanitizePath(file));

            // Validate path stays within base directory
            if (!IsPathWithinBase(basePath, fullPath))
            {
                _logger.LogWarning("Path traversal attempt detected in delete: {Folder}/{File}", folder, file);
                return BadRequest(new FileOperationResponse { Success = false, ErrorMessage = "Invalid path" });
            }

            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogWarning("File not found for delete: {Path}", fullPath);
                return NotFound(new FileOperationResponse { Success = false, ErrorMessage = "File not found" });
            }

            System.IO.File.Delete(fullPath);
            _logger.LogInformation("Deleted file {Path}", fullPath);

            return Ok(new FileOperationResponse { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {VirtualDir}/{Folder}/{File}", virtualDir, folder, file);
            return StatusCode(500, new FileOperationResponse { Success = false, ErrorMessage = "An error occurred while deleting the file" });
        }
    }

    #endregion Delete Operations

    #region Helper Methods

    /// <summary>
    /// Gets the storage path for a virtual directory from configuration.
    /// Returns null if the virtual directory is not configured.
    /// </summary>
    private string? GetStoragePath(string virtualDir)
    {
        var configKey = $"VirtualDirectories:{virtualDir}";
        var path = _configuration[configKey];

        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Log warning when requested directory is not configured
        _logger.LogWarning("Virtual directory '{VirtualDir}' not configured, no fallback applied", virtualDir);
        return null;
    }

    /// <summary>
    /// Sanitizes a path to prevent directory traversal attacks.
    /// </summary>
    private static string SanitizePath(string path)
    {
        var sanitized = path
            .Replace("..", "")
            .Replace("//", "/")
            .Replace("\\\\", "\\")
            .Trim('/', '\\');

        return sanitized.Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Validates that the resolved path stays within the base directory.
    /// </summary>
    private static bool IsPathWithinBase(string basePath, string fullPath)
    {
        var resolvedBase = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolvedPath = Path.GetFullPath(fullPath);
        return resolvedPath.StartsWith(resolvedBase, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sanitizes a filename to remove path traversal characters and invalid characters.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        // Get just the filename without any path
        var name = Path.GetFileName(fileName);

        // Remove any remaining invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            name = name.Replace(c.ToString(), "");
        }

        // Ensure we have a valid filename
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"file_{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        return name;
    }

    /// <summary>
    /// Gets the MIME content type based on file extension.
    /// </summary>
    private static string GetContentTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    #endregion Helper Methods
}
