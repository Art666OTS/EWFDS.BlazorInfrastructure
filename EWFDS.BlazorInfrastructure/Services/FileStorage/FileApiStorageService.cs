using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EWFDS.BlazorInfrastructure.Services.FileStorage;

/// <summary>
/// File storage service that calls the external File API.
/// Generic implementation that can be used by any application.
/// </summary>
public class FileApiStorageService : IFileApiStorageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FileApiSettings _settings;
    private readonly ILogger<FileApiStorageService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FileApiStorageService(
        IHttpClientFactory httpClientFactory,
        IOptions<FileApiSettings> settings,
        ILogger<FileApiStorageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(bool IsHealthy, string Message)> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/files/health";

        try
        {
            _logger.LogInformation("Checking File API health at {Url}", url);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5)); // Short timeout for health check

            var response = await SendRequestAsync(HttpMethod.Get, url, content: null, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("File API health check passed");
                return (true, "File API is online");
            }
            else
            {
                var message = $"File API returned {response.StatusCode}";
                _logger.LogWarning("File API health check failed: {Message}", message);
                return (false, message);
            }
        }
        catch (TaskCanceledException)
        {
            var message = "File API is not responding (timeout)";
            _logger.LogWarning("File API health check timed out");
            return (false, message);
        }
        catch (HttpRequestException ex)
        {
            var message = $"Cannot connect to File API: {ex.Message}";
            _logger.LogWarning(ex, "File API health check failed");
            return (false, message);
        }
        catch (Exception ex)
        {
            var message = $"File API error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during File API health check");
            return (false, message);
        }
    }

    public async Task<FileStorageResult> UploadAsync(string virtualDir, string folder, string fileName, Stream fileStream, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(virtualDir))
            virtualDir = "Documents";

        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/files/upload?source={Uri.EscapeDataString(_settings.Source)}&virtualDir={Uri.EscapeDataString(virtualDir)}&folder={Uri.EscapeDataString(folder)}";

        _logger.LogInformation("Uploading file {FileName} to {Url}", fileName, url);

        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fileName));
        content.Add(streamContent, "file", fileName);

        try
        {
            var response = await SendRequestAsync(HttpMethod.Post, url, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully uploaded file {FileName}", fileName);

                var result = JsonSerializer.Deserialize<FileStorageResult>(responseContent, JsonOptions);
                return result ?? new FileStorageResult { Success = true, FileName = fileName, FilePath = $"{virtualDir}/{folder}/{fileName}" };
            }
            else
            {
                _logger.LogWarning("Upload failed for {FileName}: {StatusCode} - {Response}", fileName, response.StatusCode, responseContent);
                return new FileStorageResult
                {
                    Success = false,
                    ErrorMessage = $"Upload failed: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upload failed for {FileName}: {Message}", fileName, ex.Message);
            return new FileStorageResult
            {
                Success = false,
                ErrorMessage = $"Upload failed: {ex.Message}. Please try again."
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Upload timed out for {FileName}", fileName);
            return new FileStorageResult
            {
                Success = false,
                ErrorMessage = "Upload timed out. Please try again."
            };
        }
        finally
        {
            content.Dispose();
        }
    }

    public async Task<FileStorageResult> UploadAsync(string virtualDir, string folder, string fileName, byte[] fileBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(virtualDir))
            virtualDir = "Documents";

        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/files/upload?source={Uri.EscapeDataString(_settings.Source)}&virtualDir={Uri.EscapeDataString(virtualDir)}&folder={Uri.EscapeDataString(folder)}";

        _logger.LogInformation("Uploading file {FileName} ({Size} bytes) to {Url}", fileName, fileBytes.Length, url);

        var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(fileBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(fileName));
        content.Add(byteContent, "file", fileName);

        try
        {
            var response = await SendRequestAsync(HttpMethod.Post, url, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully uploaded file {FileName}", fileName);

                var result = JsonSerializer.Deserialize<FileStorageResult>(responseContent, JsonOptions);
                return result ?? new FileStorageResult { Success = true, FileName = fileName, FilePath = $"{virtualDir}/{folder}/{fileName}" };
            }
            else
            {
                _logger.LogWarning("Upload failed for {FileName}: {StatusCode} - {Response}", fileName, response.StatusCode, responseContent);
                return new FileStorageResult
                {
                    Success = false,
                    ErrorMessage = $"Upload failed: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upload failed for {FileName}: {Message}", fileName, ex.Message);
            return new FileStorageResult
            {
                Success = false,
                ErrorMessage = $"Upload failed: {ex.Message}. Please try again."
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Upload timed out for {FileName}", fileName);
            return new FileStorageResult
            {
                Success = false,
                ErrorMessage = "Upload timed out. Please try again."
            };
        }
        finally
        {
            content.Dispose();
        }
    }

    public async Task<List<FileApiFileInfo>> ListFilesAsync(string virtualDir, string folder, CancellationToken cancellationToken = default)
    {
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/files/list?source={Uri.EscapeDataString(_settings.Source)}&virtualDir={Uri.EscapeDataString(virtualDir ?? "Documents")}&folder={Uri.EscapeDataString(folder ?? "")}";

        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogInformation("Listing files from {Url}", url);

            var response = await SendRequestAsync(HttpMethod.Get, url, content: null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<FileListResponse>(content, JsonOptions);

                if (result?.Success == true && result.Files != null)
                {
                    return result.Files.Select(f => new FileApiFileInfo
                    {
                        Name = f.Name,
                        Size = f.Size,
                        LastModified = f.LastModified,
                        ContentType = f.ContentType ?? "application/octet-stream"
                    }).ToList();
                }
            }
            else
            {
                _logger.LogWarning("List files failed: {StatusCode}", response.StatusCode);
                if ((int)response.StatusCode >= 500)
                {
                    throw new HttpRequestException($"Server error: {response.StatusCode}");
                }
            }

            return new List<FileApiFileInfo>();
        }, $"List files {virtualDir}/{folder}", cancellationToken) ?? new List<FileApiFileInfo>();
    }

    public async Task<FileStorageResult> DeleteAsync(string virtualDir, string folder, string fileName, CancellationToken cancellationToken = default)
    {
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/files/delete?source={Uri.EscapeDataString(_settings.Source)}&virtualDir={Uri.EscapeDataString(virtualDir ?? "Documents")}&folder={Uri.EscapeDataString(folder ?? "")}&file={Uri.EscapeDataString(fileName)}";

        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogInformation("Deleting file {FileName} via {Url}", fileName, url);

            var response = await SendRequestAsync(HttpMethod.Delete, url, content: null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted file {FileName}", fileName);
                return new FileStorageResult { Success = true };
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Delete failed for {FileName}: {StatusCode} - {Response}", fileName, response.StatusCode, content);
                if ((int)response.StatusCode >= 500)
                {
                    throw new HttpRequestException($"Server error: {response.StatusCode}");
                }
                return new FileStorageResult
                {
                    Success = false,
                    ErrorMessage = $"Delete failed: {response.StatusCode}"
                };
            }
        }, $"Delete {fileName}", cancellationToken);
    }

    public string GetDownloadUrl(string virtualDir, string folder, string fileName)
    {
        // Return the local proxy URL - browser can access this without API key
        return $"/api/fileproxy/download?virtualDir={Uri.EscapeDataString(virtualDir ?? "Documents")}&folder={Uri.EscapeDataString(folder ?? "")}&file={Uri.EscapeDataString(fileName)}";
    }

    public async Task<(Stream? Stream, string ContentType, string? ErrorMessage)> DownloadAsync(string virtualDir, string folder, string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (null, "application/octet-stream", "File name is required");
        }

        var url = $"{_settings.BaseUrl.TrimEnd('/')}/api/files/download?source={Uri.EscapeDataString(_settings.Source)}&virtualDir={Uri.EscapeDataString(virtualDir ?? "Documents")}&folder={Uri.EscapeDataString(folder ?? "")}&file={Uri.EscapeDataString(fileName)}";

        _logger.LogInformation("Downloading file {FileName} from {Url}", fileName, url);

        try
        {
            var response = await SendRequestAsync(HttpMethod.Get, url, content: null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType ?? GetContentType(fileName);
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                _logger.LogInformation("Successfully downloaded file {FileName}", fileName);
                return (stream, contentType, null);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Download failed for {FileName}: {StatusCode} - {Response}", fileName, response.StatusCode, errorContent);
                return (null, "application/octet-stream", $"Download failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileName}", fileName);
            return (null, "application/octet-stream", $"Error downloading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends an HTTP request with the API key header set at request level (thread-safe).
    /// </summary>
    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string url, HttpContent? content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("FileApi");
        client.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-API-Key", _settings.ApiKey);

        if (content != null)
        {
            request.Content = content;
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
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

    /// <summary>
    /// Executes an operation with retry logic using exponential backoff.
    /// Retries on HttpRequestException and TaskCanceledException (timeouts).
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, CancellationToken cancellationToken)
    {
        var maxRetries = _settings.MaxRetries;
        var baseDelay = _settings.RetryDelayMs;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User-requested cancellation - don't retry
                throw;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning("Attempt {Attempt}/{MaxRetries} failed for {Operation}: {Message}", 
                    attempt, maxRetries, operationName, ex.Message);

                // Don't retry if the server is unreachable (connection refused, DNS failure, etc.)
                if (ex.InnerException is System.Net.Sockets.SocketException)
                {
                    _logger.LogWarning("Server unreachable, not retrying");
                    break;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !ex.CancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                _logger.LogWarning("Attempt {Attempt}/{MaxRetries} timed out for {Operation}", 
                    attempt, maxRetries, operationName);
            }

            if (attempt < maxRetries)
            {
                var delay = Math.Min(baseDelay * (int)Math.Pow(2, attempt - 1), 2000); // Cap at 2 seconds
                _logger.LogInformation("Retrying {Operation} in {Delay}ms...", operationName, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        _logger.LogError(lastException, "All {MaxRetries} attempts failed for {Operation}", maxRetries, operationName);
        throw lastException ?? new Exception($"Operation '{operationName}' failed after {maxRetries} attempts");
    }

    // Response model for list endpoint
    private class FileListResponse
    {
        public bool Success { get; set; }
        public List<FileListItem>? Files { get; set; }
    }

    private class FileListItem
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? ContentType { get; set; }
    }
}
