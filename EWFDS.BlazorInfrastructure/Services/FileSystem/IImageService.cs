namespace EWFDS.BlazorInfrastructure.Services.FileSystem
{
    /// <summary>
    /// Service for file operations including Base64 encoding for images and PDF documents
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// Reads an image or PDF file from disk and converts it to a Base64-encoded string
        /// </summary>
        /// <param name="filePath">The full or relative path to the image or PDF file</param>
        /// <returns>Base64-encoded string representation of the file</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be accessed</exception>
        Task<string> ConvertImageToBase64Async(string filePath);

        /// <summary>
        /// Reads an image or PDF file from disk and converts it to a Base64-encoded data URI string
        /// </summary>
        /// <param name="filePath">The full or relative path to the image or PDF file</param>
        /// <returns>Base64-encoded data URI string (e.g., "data:image/png;base64,..." or "data:application/pdf;base64,...")</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be accessed</exception>
        Task<string> ConvertImageToBase64DataUriAsync(string filePath);

        /// <summary>
        /// Reads a PDF file from disk and converts it to a Base64-encoded string
        /// </summary>
        /// <param name="filePath">The full or relative path to the PDF file</param>
        /// <returns>Base64-encoded string representation of the PDF</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be accessed</exception>
        Task<string> ConvertPdfToBase64Async(string filePath);

        /// <summary>
        /// Reads a PDF file from disk and converts it to a Base64-encoded data URI string
        /// </summary>
        /// <param name="filePath">The full or relative path to the PDF file</param>
        /// <returns>Base64-encoded data URI string (e.g., "data:application/pdf;base64,...")</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be accessed</exception>
        Task<string> ConvertPdfToBase64DataUriAsync(string filePath);

        /// <summary>
        /// Validates if a file exists and is a supported image format
        /// </summary>
        /// <param name="filePath">The full or relative path to the image file</param>
        /// <returns>True if the file exists and is a supported image format, false otherwise</returns>
        bool IsValidImageFile(string filePath);

        /// <summary>
        /// Validates if a file exists and is a PDF document
        /// </summary>
        /// <param name="filePath">The full or relative path to the PDF file</param>
        /// <returns>True if the file exists and is a PDF, false otherwise</returns>
        bool IsValidPdfFile(string filePath);

        /// <summary>
        /// Validates if a file exists and is either a supported image format or PDF
        /// </summary>
        /// <param name="filePath">The full or relative path to the file</param>
        /// <returns>True if the file exists and is a supported format, false otherwise</returns>
        bool IsValidFile(string filePath);
    }
}
