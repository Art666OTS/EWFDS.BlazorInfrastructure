using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace EWFDS.BlazorInfrastructure.Services.FileSystem
{
    /// <summary>
    /// Service for file operations including Base64 encoding for images and PDF documents
    /// </summary>
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<ImageService> _logger;

        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".ico", ".webp"
        };

        private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".gif", "image/gif" },
            { ".bmp", "image/bmp" },
            { ".svg", "image/svg+xml" },
            { ".ico", "image/x-icon" },
            { ".webp", "image/webp" },
            { ".pdf", "application/pdf" }
        };

        public ImageService(
            IWebHostEnvironment webHostEnvironment, 
            ILogger<ImageService> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        /// <summary>
        /// Reads an image or PDF file from disk and converts it to a Base64-encoded string
        /// </summary>
        /// <param name="filePath">The full or relative path to the image or PDF file</param>
        /// <returns>Base64-encoded string representation of the file</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be accessed</exception>
        public async Task<string> ConvertImageToBase64Async(string filePath)
        {
            try
            {
                string resolvedPath = ResolvePath(filePath);

                if (!File.Exists(resolvedPath))
                {
                    _logger.LogError("File not found: {FilePath}", resolvedPath);
                    throw new FileNotFoundException($"File not found: {resolvedPath}", resolvedPath);
                }

                byte[] fileBytes = await File.ReadAllBytesAsync(resolvedPath);
                string base64String = Convert.ToBase64String(fileBytes);

                _logger.LogInformation("Successfully converted file to Base64: {FilePath} ({Size} bytes)", 
                    resolvedPath, fileBytes.Length);

                return base64String;
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access to file: {FilePath}", filePath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting file to Base64: {FilePath}", filePath);
                throw new IOException($"Error reading file: {filePath}", ex);
            }
        }

        /// <summary>
        /// Reads an image or PDF file from disk and converts it to a Base64-encoded data URI string
        /// </summary>
        /// <param name="filePath">The full or relative path to the image or PDF file</param>
        /// <returns>Base64-encoded data URI string (e.g., "data:image/png;base64,..." or "data:application/pdf;base64,...")</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be accessed</exception>
        public async Task<string> ConvertImageToBase64DataUriAsync(string filePath)
        {
            string base64String = await ConvertImageToBase64Async(filePath);
            string mimeType = GetMimeType(filePath);

            return $"data:{mimeType};base64,{base64String}";
        }

        /// <summary>
        /// Reads a PDF file from disk and converts it to a Base64-encoded string
        /// </summary>
        /// <param name="filePath">The full or relative path to the PDF file</param>
        /// <returns>Base64-encoded string representation of the PDF</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be accessed</exception>
        public async Task<string> ConvertPdfToBase64Async(string filePath)
        {
            try
            {
                string resolvedPath = ResolvePath(filePath);

                if (!File.Exists(resolvedPath))
                {
                    _logger.LogError("PDF file not found: {FilePath}", resolvedPath);
                    throw new FileNotFoundException($"PDF file not found: {resolvedPath}", resolvedPath);
                }

                if (!IsValidPdfFile(filePath))
                {
                    _logger.LogWarning("File is not a PDF: {FilePath}", resolvedPath);
                    throw new InvalidOperationException($"File is not a PDF document: {filePath}");
                }

                byte[] pdfBytes = await File.ReadAllBytesAsync(resolvedPath);
                string base64String = Convert.ToBase64String(pdfBytes);

                _logger.LogInformation("Successfully converted PDF to Base64: {FilePath} ({Size} bytes)", 
                    resolvedPath, pdfBytes.Length);

                return base64String;
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access to PDF file: {FilePath}", filePath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting PDF to Base64: {FilePath}", filePath);
                throw new IOException($"Error reading PDF file: {filePath}", ex);
            }
        }

        /// <summary>
        /// Reads a PDF file from disk and converts it to a Base64-encoded data URI string
        /// </summary>
        /// <param name="filePath">The full or relative path to the PDF file</param>
        /// <returns>Base64-encoded data URI string (e.g., "data:application/pdf;base64,...")</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be accessed</exception>
        public async Task<string> ConvertPdfToBase64DataUriAsync(string filePath)
        {
            string base64String = await ConvertPdfToBase64Async(filePath);
            return $"data:application/pdf;base64,{base64String}";
        }

        /// <summary>
        /// Validates if a file exists and is a supported image format
        /// </summary>
        /// <param name="filePath">The full or relative path to the image file</param>
        /// <returns>True if the file exists and is a supported image format, false otherwise</returns>
        public bool IsValidImageFile(string filePath)
        {
            try
            {
                string resolvedPath = ResolvePath(filePath);

                if (!File.Exists(resolvedPath))
                {
                    return false;
                }

                string extension = Path.GetExtension(resolvedPath);
                return SupportedImageExtensions.Contains(extension);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating image file: {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Validates if a file exists and is a PDF document
        /// </summary>
        /// <param name="filePath">The full or relative path to the PDF file</param>
        /// <returns>True if the file exists and is a PDF, false otherwise</returns>
        public bool IsValidPdfFile(string filePath)
        {
            try
            {
                string resolvedPath = ResolvePath(filePath);

                if (!File.Exists(resolvedPath))
                {
                    return false;
                }

                string extension = Path.GetExtension(resolvedPath);
                return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating PDF file: {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Validates if a file exists and is either a supported image format or PDF
        /// </summary>
        /// <param name="filePath">The full or relative path to the file</param>
        /// <returns>True if the file exists and is a supported format, false otherwise</returns>
        public bool IsValidFile(string filePath)
        {
            return IsValidImageFile(filePath) || IsValidPdfFile(filePath);
        }

        /// <summary>
        /// Resolves a file path to a full physical path
        /// Handles relative paths, virtual paths (starting with ~), and absolute paths
        /// </summary>
        private string ResolvePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            // Handle virtual paths (~/...)
            if (filePath.StartsWith("~/") || filePath.StartsWith("~\\"))
            {
                string relativePath = filePath[2..].Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(_webHostEnvironment.WebRootPath, relativePath);
            }

            // Handle absolute paths
            if (Path.IsPathRooted(filePath))
                return filePath;

            // Handle relative paths (assume relative to wwwroot)
            return Path.Combine(_webHostEnvironment.WebRootPath, filePath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Gets the MIME type for a file based on its extension
        /// </summary>
        private string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath);

            if (MimeTypes.TryGetValue(extension, out string? mimeType))
            {
                return mimeType;
            }

            // Default fallback
            return "application/octet-stream";
        }
    }
}
