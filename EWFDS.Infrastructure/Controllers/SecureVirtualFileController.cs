using EWFDS.Infrastructure.Common.FileSystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;

namespace EWFDS.Infrastructure.Controllers;

/// <summary>
/// Secure file serving endpoint for virtual directories.
/// Replaces the open static file middleware with an authorized endpoint.
/// URL pattern: /virtual/{virtualDir}/{**filePath}
/// </summary>
[Route("virtual")]
[ApiController]
[Authorize]
public class SecureVirtualFileController : ControllerBase
{
    private readonly ILogger<SecureVirtualFileController> _logger;
    private readonly IVirtualDirectoryService _virtualDirectoryService;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;

    public SecureVirtualFileController(
        ILogger<SecureVirtualFileController> logger,
        IVirtualDirectoryService virtualDirectoryService)
    {
        _logger = logger;
        _virtualDirectoryService = virtualDirectoryService;
        _contentTypeProvider = new FileExtensionContentTypeProvider();
    }

    /// <summary>
    /// Serves a file from a virtual directory with authorization.
    /// Usage: GET /virtual/{virtualDir}/{filePath}
    /// Example: GET /virtual/Documents/orders/invoice.pdf
    /// </summary>
    [HttpGet("{virtualDir}/{**filePath}")]
    public IActionResult GetFile(string virtualDir, string filePath)
    {
        if (string.IsNullOrWhiteSpace(virtualDir))
        {
            return BadRequest("Virtual directory is required");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest("File path is required");
        }

        // Check if virtual directory is configured
        if (!_virtualDirectoryService.IsVirtualDirectoryConfigured(virtualDir))
        {
            _logger.LogWarning("Virtual directory not configured: {VirtualDir}", virtualDir);
            return NotFound("Virtual directory not found");
        }

        try
        {
            // Get the physical root for this virtual directory
            var physicalRoot = _virtualDirectoryService.GetPhysicalRoot(virtualDir);

            // Sanitize the file path to prevent directory traversal attacks
            var sanitizedPath = SanitizePath(filePath);
            var fullPath = Path.Combine(physicalRoot, sanitizedPath);

            // Validate path stays within the virtual directory root
            var fullPathInfo = new FileInfo(fullPath);
            var rootInfo = new DirectoryInfo(physicalRoot);

            if (!fullPathInfo.FullName.StartsWith(rootInfo.FullName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Path traversal attempt detected: {VirtualDir}/{FilePath}", virtualDir, filePath);
                return BadRequest("Invalid file path");
            }

            // Check if file exists
            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogDebug("File not found: {FullPath}", fullPath);
                return NotFound("File not found");
            }

            // Get content type
            if (!_contentTypeProvider.TryGetContentType(fullPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            _logger.LogInformation("Serving file: {VirtualDir}/{FilePath} to user {User}",
                virtualDir, filePath, User.Identity?.Name ?? "Unknown");

            // Open file stream and return
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Use inline disposition for viewable file types
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (CanDisplayInline(extension))
            {
                Response.Headers.ContentDisposition = $"inline; filename=\"{Path.GetFileName(fullPath)}\"";
                return File(stream, contentType);
            }

            // For other file types, trigger download
            return File(stream, contentType, Path.GetFileName(fullPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving file: {VirtualDir}/{FilePath}", virtualDir, filePath);
            return StatusCode(500, "An error occurred while serving the file");
        }
    }

    /// <summary>
    /// Sanitizes a file path to prevent directory traversal attacks.
    /// </summary>
    private static string SanitizePath(string path)
    {
        // Replace backslashes with forward slashes
        path = path.Replace('\\', '/');

        // Remove any directory traversal sequences
        path = path.Replace("..", string.Empty);

        // Remove leading slashes
        path = path.TrimStart('/');

        // Split and rejoin to normalize
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine(segments);
    }

    /// <summary>
    /// Checks if the file type can be displayed inline in browser/iframe.
    /// </summary>
    private static bool CanDisplayInline(string extension)
    {
        return extension is ".pdf" or ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp"
            or ".txt" or ".html" or ".htm" or ".xml" or ".json" or ".svg";
    }
}
