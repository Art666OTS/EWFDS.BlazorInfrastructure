using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EWFDS.BlazorInfrastructure.Services.FileSystem
{
    /// <summary>
    /// Service for resolving virtual directory paths to physical disk locations
    /// Similar to IIS Virtual Directories
    /// </summary>
    public interface IVirtualDirectoryService
    {
        /// <summary>
        /// Resolves a virtual path to a physical file path
        /// </summary>
        /// <param name="virtualPath">Virtual path (e.g., "/Images/myfile.jpg")</param>
        /// <returns>Full physical path to the file</returns>
        string ResolvePhysicalPath(string virtualPath);

        /// <summary>
        /// Gets the physical root path for a virtual directory
        /// </summary>
        /// <param name="virtualDirectoryName">Name of the virtual directory (e.g., "Images")</param>
        /// <returns>Full physical path to the directory</returns>
        string GetPhysicalRoot(string virtualDirectoryName);

        /// <summary>
        /// Checks if a virtual directory is configured
        /// </summary>
        /// <param name="virtualDirectoryName">Name of the virtual directory</param>
        /// <returns>True if configured, false otherwise</returns>
        bool IsVirtualDirectoryConfigured(string virtualDirectoryName);

        /// <summary>
        /// Gets all configured virtual directory names
        /// </summary>
        IEnumerable<string> GetConfiguredVirtualDirectories();
    }

    public class VirtualDirectoryService : IVirtualDirectoryService
    {
        private readonly Dictionary<string, string> _virtualDirectories;
        private readonly ILogger<VirtualDirectoryService> _logger;

        public VirtualDirectoryService(IConfiguration configuration, ILogger<VirtualDirectoryService> logger)
        {
            _logger = logger;
            _virtualDirectories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Load virtual directories from configuration
            var virtualDirSection = configuration.GetSection("VirtualDirectories");
            foreach (var child in virtualDirSection.GetChildren())
            {
                var name = child.Key;
                var path = child.Value;

                if (!string.IsNullOrWhiteSpace(path))
                {
                    _virtualDirectories[name] = path;
                    _logger.LogInformation("Registered virtual directory '{Name}' -> '{Path}'", name, path);
                }
            }
        }

        public string ResolvePhysicalPath(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath))
            {
                throw new ArgumentException("Virtual path cannot be null or empty", nameof(virtualPath));
            }

            // Normalize path separators
            virtualPath = virtualPath.Replace('\\', '/').TrimStart('/');

            // Split path into segments
            var segments = virtualPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                throw new ArgumentException("Invalid virtual path", nameof(virtualPath));
            }

            var virtualDirName = segments[0];

            if (!_virtualDirectories.TryGetValue(virtualDirName, out var physicalRoot))
            {
                throw new InvalidOperationException($"Virtual directory '{virtualDirName}' is not configured");
            }

            // Combine physical root with remaining path segments
            var relativePath = string.Join(Path.DirectorySeparatorChar, segments.Skip(1));
            var fullPath = Path.Combine(physicalRoot, relativePath);

            // Security: Ensure the resolved path is within the virtual directory root
            var normalizedRoot = Path.GetFullPath(physicalRoot);
            var normalizedPath = Path.GetFullPath(fullPath);

            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Path traversal detected");
            }

            return normalizedPath;
        }

        public string GetPhysicalRoot(string virtualDirectoryName)
        {
            if (string.IsNullOrWhiteSpace(virtualDirectoryName))
            {
                throw new ArgumentException("Virtual directory name cannot be null or empty", nameof(virtualDirectoryName));
            }

            if (!_virtualDirectories.TryGetValue(virtualDirectoryName, out var physicalRoot))
            {
                throw new InvalidOperationException($"Virtual directory '{virtualDirectoryName}' is not configured");
            }

            return physicalRoot;
        }

        public bool IsVirtualDirectoryConfigured(string virtualDirectoryName)
        {
            return !string.IsNullOrWhiteSpace(virtualDirectoryName) && 
                   _virtualDirectories.ContainsKey(virtualDirectoryName);
        }

        public IEnumerable<string> GetConfiguredVirtualDirectories()
        {
            return _virtualDirectories.Keys.ToList();
        }
    }
}
