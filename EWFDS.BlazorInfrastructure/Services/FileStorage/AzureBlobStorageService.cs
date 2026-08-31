using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EWFDS.BlazorInfrastructure.Services.FileStorage;

/// <summary>
/// Azure Blob Storage implementation of IFileApiStorageService using Managed Identity.
/// </summary>
public class AzureBlobStorageService : IFileApiStorageService
{
    private readonly AzureBlobStorageSettings _settings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public AzureBlobStorageService(IOptions<AzureBlobStorageSettings> settings, IHostEnvironment environment, ILogger<AzureBlobStorageService> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;

        // Determine container based on environment
        _containerName = GetContainerName();

        // Create BlobServiceClient using Managed Identity or Connection String
        _blobServiceClient = CreateBlobServiceClient();

        _logger.LogInformation("AzureBlobStorageService initialized for environment {Environment} with container {Container}",
            _environment.EnvironmentName, _containerName);
    }

    private string GetContainerName()
    {
        return _environment.EnvironmentName.ToLowerInvariant() switch
        {
            "development" => _settings.Containers.Development,
            "staging" => _settings.Containers.Staging,
            "production" => _settings.Containers.Production,
            _ => _settings.Containers.Development // Default to development for unknown environments
        };
    }

    private BlobServiceClient CreateBlobServiceClient()
    {
        // If connection string is provided and we're not forcing Managed Identity, use it
        if (!string.IsNullOrWhiteSpace(_settings.ConnectionString) && !_settings.UseManagedIdentity)
        {
            _logger.LogInformation("Using connection string for Azure Blob Storage authentication");
            return new BlobServiceClient(_settings.ConnectionString);
        }

        // Use Managed Identity (DefaultAzureCredential handles multiple auth methods)
        if (string.IsNullOrWhiteSpace(_settings.AccountName))
        {
            throw new InvalidOperationException("AzureBlobStorage:AccountName must be configured when using Managed Identity.");
        }

        var blobUri = new Uri($"https://{_settings.AccountName}.blob.core.windows.net");

        // DefaultAzureCredential tries multiple auth methods in order:
        // 1. Environment variables
        // 2. Managed Identity (in Azure)
        // 3. Visual Studio / VS Code credentials (for local dev)
        // 4. Azure CLI credentials (for local dev)
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true // Don't prompt for browser login
        });

        _logger.LogInformation("Using DefaultAzureCredential (Managed Identity) for Azure Blob Storage authentication to {AccountName}", _settings.AccountName);

        return new BlobServiceClient(blobUri, credential);
    }

    /// <inheritdoc />
    public async Task<(bool IsHealthy, string Message)> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var exists = await containerClient.ExistsAsync(cancellationToken);

            if (exists.Value)
            {
                return (true, $"Azure Blob Storage connected. Container '{_containerName}' is accessible.");
            }
            else
            {
                return (false, $"Container '{_containerName}' does not exist. Please create it in Azure Portal.");
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Blob Storage health check failed: {Message}", ex.Message);
            return (false, $"Azure Blob Storage error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Blob Storage health check failed with unexpected error");
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<FileStorageResult> UploadAsync(string virtualDir, string folder, string fileName, Stream fileStream, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobPath = BuildBlobPath(virtualDir, folder, fileName);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            _logger.LogInformation("Uploading blob {BlobPath} to container {Container}", blobPath, _containerName);

            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            // Upload with overwrite
            await blobClient.UploadAsync(fileStream, overwrite: true, cancellationToken);

            _logger.LogInformation("Successfully uploaded blob {BlobPath}", blobPath);

            return new FileStorageResult
            {
                Success = true,
                FileName = fileName,
                FilePath = blobPath
            };
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to upload blob {FileName}: {Message}", fileName, ex.Message);
            return new FileStorageResult
            {
                Success = false,
                FileName = fileName,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading blob {FileName}", fileName);
            return new FileStorageResult
            {
                Success = false,
                FileName = fileName,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<FileStorageResult> UploadAsync(string virtualDir, string folder, string fileName, byte[] fileBytes, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(fileBytes);
        return await UploadAsync(virtualDir, folder, fileName, stream, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<FileApiFileInfo>> ListFilesAsync(string virtualDir, string folder, CancellationToken cancellationToken = default)
    {
        var files = new List<FileApiFileInfo>();

        try
        {
            var prefix = BuildBlobPath(virtualDir, folder, "");
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            _logger.LogInformation("Listing blobs with prefix {Prefix} in container {Container}", prefix, _containerName);

            await foreach (var blobItem in containerClient.GetBlobsAsync(traits: BlobTraits.Metadata, states: BlobStates.None, prefix: prefix, cancellationToken: cancellationToken))
            {
                files.Add(new FileApiFileInfo
                {
                    Name = Path.GetFileName(blobItem.Name),
                    Size = blobItem.Properties.ContentLength ?? 0,
                    LastModified = blobItem.Properties.LastModified?.DateTime ?? DateTime.MinValue,
                    ContentType = blobItem.Properties.ContentType ?? "application/octet-stream"
                });
            }

            _logger.LogInformation("Found {Count} blobs with prefix {Prefix}", files.Count, prefix);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to list blobs with prefix: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing blobs");
        }

        return files;
    }

    /// <inheritdoc />
    public async Task<FileStorageResult> DeleteAsync(string virtualDir, string folder, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobPath = BuildBlobPath(virtualDir, folder, fileName);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            _logger.LogInformation("Deleting blob {BlobPath} from container {Container}", blobPath, _containerName);

            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            if (response.Value)
            {
                _logger.LogInformation("Successfully deleted blob {BlobPath}", blobPath);
                return new FileStorageResult
                {
                    Success = true,
                    FileName = fileName,
                    FilePath = blobPath
                };
            }
            else
            {
                _logger.LogWarning("Blob {BlobPath} did not exist", blobPath);
                return new FileStorageResult
                {
                    Success = false,
                    FileName = fileName,
                    ErrorMessage = "File not found"
                };
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to delete blob {FileName}: {Message}", fileName, ex.Message);
            return new FileStorageResult
            {
                Success = false,
                FileName = fileName,
                ErrorMessage = $"Delete failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting blob {FileName}", fileName);
            return new FileStorageResult
            {
                Success = false,
                FileName = fileName,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<(Stream? Stream, string ContentType, string? ErrorMessage)> DownloadAsync(string virtualDir, string folder, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobPath = BuildBlobPath(virtualDir, folder, fileName);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            _logger.LogInformation("Downloading blob {BlobPath} from container {Container}", blobPath, _containerName);

            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return (null, "application/octet-stream", "File not found");
            }

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            var contentType = response.Value.Details.ContentType ?? "application/octet-stream";

            _logger.LogInformation("Successfully downloaded blob {BlobPath}", blobPath);

            return (response.Value.Content, contentType, null);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to download blob {FileName}: {Message}", fileName, ex.Message);
            return (null, "application/octet-stream", $"Download failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading blob {FileName}", fileName);
            return (null, "application/octet-stream", $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public string GetDownloadUrl(string virtualDir, string folder, string fileName)
    {
        // For Azure Blob Storage with Managed Identity, we can't generate SAS tokens directly
        // Return a proxy URL that the controller will handle
        return $"/api/filestorage/download/{Uri.EscapeDataString(virtualDir)}/{Uri.EscapeDataString(folder)}/{Uri.EscapeDataString(fileName)}";
    }

    /// <summary>
    /// Generates a SAS URL for direct blob access.
    /// Note: Requires storage account key (not available with Managed Identity alone).
    /// </summary>
    public Uri? GenerateSasUrl(string virtualDir, string folder, string fileName, int? validityHours = null)
    {
        try
        {
            var blobPath = BuildBlobPath(virtualDir, folder, fileName);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!blobClient.CanGenerateSasUri)
            {
                _logger.LogWarning(
                    "Cannot generate SAS URL for blob {BlobPath}. " +
                    "Use the proxy download endpoint instead when using Managed Identity.",
                    blobPath);
                return null;
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerName,
                BlobName = blobPath,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(validityHours ?? _settings.SasTokenValidityHours)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            return blobClient.GenerateSasUri(sasBuilder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL for {FileName}", fileName);
            return null;
        }
    }

    /// <summary>
    /// Builds the blob path from virtual directory, folder, and filename.
    /// </summary>
    private static string BuildBlobPath(string virtualDir, string folder, string fileName)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(virtualDir))
        {
            parts.Add(virtualDir.Trim('/').Replace('\\', '/'));
        }

        if (!string.IsNullOrWhiteSpace(folder))
        {
            parts.Add(folder.Trim('/').Replace('\\', '/'));
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            parts.Add(fileName);
        }

        return string.Join("/", parts);
    }
}
