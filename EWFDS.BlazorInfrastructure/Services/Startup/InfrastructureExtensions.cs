using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using System;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using EWFDS.BlazorInfrastructure.Services.FileStorage;
using EWFDS.BlazorInfrastructure.Services.FileSystem;
using Microsoft.AspNetCore.SignalR;
using System.Reflection;

namespace EWFDS.BlazorInfrastructure.Services.Startup
{
    public static class InfrastructureExtensions
    {
        /// <summary>
        /// Configure Serilog using settings from configuration and return the logs directory path used.
        /// </summary>
        public static string ConfigureSerilog(this WebApplicationBuilder builder)
        {
            var serilogSection = builder.Configuration.GetSection("SerilogSettings");
            string? configuredPath = serilogSection.GetValue<string?>("LogsPath");
            bool useAppServicePath = serilogSection.GetValue<bool?>("UseAppServiceLogPath") ?? true;
            long fileSizeLimitBytes = serilogSection.GetValue<long?>("FileSizeLimitBytes") ?? 10 * 1024 * 1024;
            int retainedFileCountLimit = serilogSection.GetValue<int?>("RetainedFileCountLimit") ?? 30;

            string logsDirPath;
            var home = Environment.GetEnvironmentVariable("HOME");
            if (useAppServicePath && !string.IsNullOrWhiteSpace(home))
            {
                logsDirPath = Path.Combine(home, "LogFiles", "Application");
            }
            else if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                logsDirPath = configuredPath!;
            }
            else
            {
                logsDirPath = Path.Combine(builder.Environment.ContentRootPath, "Logs");
            }

            Directory.CreateDirectory(logsDirPath);

            var infoPath = Path.Combine(logsDirPath, "info-.log");
            var warnPath = Path.Combine(logsDirPath, "warn-.log");
            var errorPath = Path.Combine(logsDirPath, "error-.log");
            var fatalPath = Path.Combine(logsDirPath, "fatal-.log");

            // Base logger configuration
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext();

            // Enable Serilog internal self-log to help diagnose sink/file errors which can
            // prevent messages from being written. Write self-log to a small file inside the
            // chosen logs directory so it is inspectable via Kudu / file system.
            try
            {
                var selfLogPath = Path.Combine(logsDirPath, "serilog-selflog.txt");
                SelfLog.Enable(msg =>
                {
                    try
                    {
                        File.AppendAllText(selfLogPath, DateTime.UtcNow.ToString("o") + " " + msg + Environment.NewLine);
                    }
                    catch
                    {
                        // swallow: self-log must not throw
                    }
                });
            }
            catch
            {
                // ignore any failures enabling self-log
            }

            // Apply category overrides from configuration if present, otherwise apply sensible defaults
            var overrideSection = builder.Configuration.GetSection("Serilog:MinimumLevel:Override");
            if (overrideSection.Exists())
            {
                foreach (var child in overrideSection.GetChildren())
                {
                    if (Enum.TryParse<LogEventLevel>(child.Value, true, out var level))
                    {
                        loggerConfig = loggerConfig.MinimumLevel.Override(child.Key, level);
                    }
                }
            }
            else
            {
                // Reduce noisy framework logs by default
                loggerConfig = loggerConfig
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);
            }

            // File sinks (non-overlapping by level using expression filters)
            // Use predicate filters to ensure each file receives only the intended level
            loggerConfig = loggerConfig
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information)
                    .WriteTo.Async(w => w.File(infoPath,
                        rollingInterval: Serilog.RollingInterval.Day,
                        fileSizeLimitBytes: fileSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: retainedFileCountLimit,
                        shared: true)))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Warning)
                    .WriteTo.Async(w => w.File(warnPath,
                        rollingInterval: Serilog.RollingInterval.Day,
                        fileSizeLimitBytes: fileSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: retainedFileCountLimit,
                        shared: true)))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                    .WriteTo.Async(w => w.File(errorPath,
                        rollingInterval: Serilog.RollingInterval.Day,
                        fileSizeLimitBytes: fileSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: retainedFileCountLimit,
                        shared: true)))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Fatal)
                    .WriteTo.Async(w => w.File(fatalPath,
                        rollingInterval: Serilog.RollingInterval.Day,
                        fileSizeLimitBytes: fileSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: retainedFileCountLimit,
                        shared: true)));

            // Also write to console so Azure Log Stream and similar capture startup messages even
            // when file sinks fail. Console is useful for diagnostics during staging/production.
            loggerConfig = loggerConfig.WriteTo.Console();

            Log.Logger = loggerConfig.CreateLogger();

            // Replace default logging providers with Serilog
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(dispose: true);

            return logsDirPath;
        }

        /// <summary>
        /// Run startup validation checks and map health endpoints. Throws if a fatal configuration or connectivity issue is detected.
        /// </summary>
        public static WebApplication ValidateInfrastructure(this WebApplication app, string logsDirPath)
        {
            // Validate SignalR
            var hubOptions = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.SignalR.HubOptions>>();
            if (hubOptions.Value.MaximumReceiveMessageSize == null || hubOptions.Value.MaximumReceiveMessageSize <= 32 * 1024)
            {
                var msg = "SignalR MaximumReceiveMessageSize must be configured for file uploads. Default is 32KB which causes silent hangs. Add: builder.Services.Configure<HubOptions>(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024);";
                Log.Fatal(msg);
                throw new InvalidOperationException(msg);
            }

            // Validate RemoteAPI
            var remoteApi = app.Configuration["SystemSettings:RemoteAPI"];
            if (string.IsNullOrWhiteSpace(remoteApi))
            {
                var msg = "SystemSettings:RemoteAPI must be configured in appsettings.json. This is required for eWFDSData HTTP client.";
                Log.Fatal(msg);
                throw new InvalidOperationException(msg);
            }

            // Validate ApplicationDB
            var appDbConn = app.Configuration.GetConnectionString("ApplicationDB");
            if (!string.IsNullOrWhiteSpace(appDbConn))
            {
                try
                {
                    using var conn = new SqlConnection(appDbConn);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    cmd.CommandTimeout = 5;
                    cmd.ExecuteScalar();
                }
                catch (Exception ex)
                {
                    var msg = $"Unable to connect to ApplicationDB: {ex.Message}";
                    Log.Fatal(ex, msg);
                    throw new InvalidOperationException(msg, ex);
                }
            }
            else
            {
                var msg = "Connection string 'ApplicationDB' must be configured (user secrets or appsettings).";
                Log.Fatal(msg);
                throw new InvalidOperationException(msg);
            }

            // Validate ReportDB
            var reportDbConn = app.Configuration.GetConnectionString("ReportDB");
            if (!string.IsNullOrWhiteSpace(reportDbConn))
            {
                try
                {
                    using var conn = new SqlConnection(reportDbConn);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    cmd.CommandTimeout = 5;
                    cmd.ExecuteScalar();
                }
                catch (Exception ex)
                {
                    var msg = $"Unable to validate ReportDB read access: {ex.Message}";
                    Log.Fatal(ex, msg);
                    throw new InvalidOperationException(msg, ex);
                }
            }
            else
            {
                var msg = "Connection string 'ReportDB' must be configured (user secrets or appsettings).";
                Log.Fatal(msg);
                throw new InvalidOperationException(msg);
            }

            // Validate File Storage configuration
            var storageProvider = app.Configuration.GetValue<string>("FileStorage:Provider") ?? "FileApi";
            if (storageProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
            {
                var azureBlobSection = app.Configuration.GetSection("AzureBlobStorage");
                if (!azureBlobSection.Exists())
                {
                    var msg = "AzureBlobStorage section must be configured in appsettings.json when FileStorage:Provider is set to 'AzureBlob'.";
                    Log.Fatal(msg);
                    throw new InvalidOperationException(msg);
                }
                var azureSettings = azureBlobSection.Get<AzureBlobStorageSettings>();
                if (azureSettings == null)
                {
                    var msg = "AzureBlobStorage section could not be parsed.";
                    Log.Fatal(msg);
                    throw new InvalidOperationException(msg);
                }
                if (azureSettings.UseManagedIdentity && string.IsNullOrWhiteSpace(azureSettings.AccountName))
                {
                    var msg = "AzureBlobStorage:AccountName must be configured when UseManagedIdentity is true.";
                    Log.Fatal(msg);
                    throw new InvalidOperationException(msg);
                }
                if (!azureSettings.UseManagedIdentity && string.IsNullOrWhiteSpace(azureSettings.ConnectionString))
                {
                    var msg = "AzureBlobStorage:ConnectionString must be configured when UseManagedIdentity is false.";
                    Log.Fatal(msg);
                    throw new InvalidOperationException(msg);
                }
            }
            else
            {
                var fileApiSection = app.Configuration.GetSection("FileApi");
                if (!fileApiSection.Exists())
                {
                    var msg = "FileApi section must be configured in appsettings.json. This is required for file storage operations.";
                    Log.Fatal(msg);
                    throw new InvalidOperationException(msg);
                }
                var fileApiSettings = fileApiSection.Get<FileApiSettings>();
                if (fileApiSettings == null || string.IsNullOrWhiteSpace(fileApiSettings.BaseUrl) || string.IsNullOrWhiteSpace(fileApiSettings.Source) || string.IsNullOrWhiteSpace(fileApiSettings.ApiKey))
                {
                    var msg = "FileApi section is missing required properties: BaseUrl, Source, and ApiKey must all be configured.";
                    Log.Fatal(msg);
                    throw new InvalidOperationException(msg);
                }
            }

            // Virtual directories are now served securely via SecureVirtualFileController.
            // Verify the virtual directory configuration and log the configured mappings. Treat missing
            // IVirtualDirectoryService as a fatal startup error so misconfiguration is visible in logs.
            try
            {
                var virtualDirService = app.Services.GetService<IVirtualDirectoryService>();
                if (virtualDirService == null)
                {
                    var msg = "IVirtualDirectoryService is not registered. Virtual directory support requires registering the service.";
                    Log.Fatal(msg);
                    throw new InvalidOperationException(msg);
                }

                foreach (var virtualDirName in virtualDirService.GetConfiguredVirtualDirectories())
                {
                    try
                    {
                        var physicalPath = virtualDirService.GetPhysicalRoot(virtualDirName);
                        if (Directory.Exists(physicalPath))
                        {
                            Log.Information("Virtual directory '{VirtualDir}' configured at '/virtual/{VirtualDir}' (secured via controller)", virtualDirName, virtualDirName);
                        }
                        else
                        {
                            Log.Warning("Physical path '{PhysicalPath}' for virtual directory '{VirtualDir}' does not exist", physicalPath, virtualDirName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to inspect virtual directory '{VirtualDir}'", virtualDirName);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fatal: ensure the error is logged and startup fails visibly
                Log.Fatal(ex, "Virtual directory initialization failed");
                throw;
            }

            // Resolve the running code version once so it can be reported by the health endpoint.
            // Prefer the informational version (may include git hash/suffix), fall back to the
            // numeric assembly version, then to "unknown".
            // Report the running app's numeric assembly version (e.g. "1.4.2.0").
            // Use the entry assembly so this reports the host app (PickPack) rather than
            // this shared infrastructure library. The assembly version never contains the
            // git commit suffix, so no stripping is required.
            var entryAssembly = Assembly.GetEntryAssembly();
            var appName = entryAssembly?.GetName().Name ?? "unknown";
            var appVersion = entryAssembly?.GetName().Version?.ToString() ?? "unknown";

            // Map health endpoint for logs
            app.MapGet("/health/logs", () =>
            {
                var exists = Directory.Exists(logsDirPath);
                int fileCount = 0;
                string latestFile = string.Empty;
                DateTime? latestWrite = null;

                if (exists)
                {
                    var files = Directory.GetFiles(logsDirPath);
                    fileCount = files.Length;
                    foreach (var f in files)
                    {
                        var wt = File.GetLastWriteTimeUtc(f);
                        if (latestWrite == null || wt > latestWrite.Value)
                        {
                            latestWrite = wt;
                            latestFile = Path.GetFileName(f);
                        }
                    }
                }

                return Results.Json(new
                {
                    Application = appName,
                    Version = appVersion,
                    LogsPath = logsDirPath,
                    Exists = exists,
                    FileCount = fileCount,
                    LatestFile = latestFile,
                    LatestWriteUtc = latestWrite?.ToString("o")
                });
            });

            // Live readiness endpoint. Actively verifies the app is up and that its critical
            // dependencies (SQL databases) are reachable *right now* (unlike the one-time
            // startup validation). Returns HTTP 200 when healthy, 503 when any check fails.
            app.MapGet("/health", async () =>
            {
                // Local helper: run "SELECT 1" against a connection string and report the result.
                static async Task<(string Status, string? Error)> CheckSqlAsync(string? connectionString)
                {
                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        return ("NotConfigured", "Connection string is not configured");
                    }

                    try
                    {
                        await using var conn = new SqlConnection(connectionString);
                        await conn.OpenAsync();
                        await using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT 1";
                        cmd.CommandTimeout = 5;
                        await cmd.ExecuteScalarAsync();
                        return ("Healthy", null);
                    }
                    catch (Exception ex)
                    {
                        return ("Unhealthy", ex.Message);
                    }
                }

                var appDb = await CheckSqlAsync(app.Configuration.GetConnectionString("ApplicationDB"));
                var reportDb = await CheckSqlAsync(app.Configuration.GetConnectionString("ReportDB"));

                var healthy = appDb.Status == "Healthy" && reportDb.Status == "Healthy";

                var payload = new
                {
                    Status = healthy ? "Healthy" : "Unhealthy",
                    Application = appName,
                    Version = appVersion,
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    Checks = new
                    {
                        ApplicationDB = new { appDb.Status, appDb.Error },
                        ReportDB = new { reportDb.Status, reportDb.Error }
                    }
                };

                // 200 when healthy, 503 (Service Unavailable) when any dependency is down.
                return Results.Json(payload, statusCode: healthy ? 200 : 503);
            });

            // Virtual directory inspection is performed above in this shared infrastructure
            // method, so callers (e.g. Program.cs) do not need to repeat it.

            // Emit a clear readiness message after all startup validation succeeded
            try
            {
                var env = app.Environment.EnvironmentName ?? "Unknown";
                string urls;
                try
                {
                    var server = app.Services.GetService<IServer>();
                    var addressesFeature = server?.Features?.Get<IServerAddressesFeature>();
                    if (addressesFeature != null && addressesFeature.Addresses != null && addressesFeature.Addresses.Count > 0)
                    {
                        urls = string.Join(',', addressesFeature.Addresses);
                    }
                    else if (app.Urls != null && app.Urls.Count > 0)
                    {
                        urls = string.Join(',', app.Urls);
                    }
                    else
                    {
                        urls = "N/A";
                    }
                }
                catch
                {
                    urls = "N/A";
                }

                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

                Log.Information("Application started and healthy at {StartedAt} (env={Environment}, urls={Urls}, logs={LogsPath}, pid={Pid})",
                    DateTime.UtcNow, env, urls, logsDirPath, pid);
            }
            catch (Exception ex)
            {
                // Non-fatal: ensure readiness logging does not prevent app startup
                Log.Warning(ex, "Failed to emit application readiness log message");
            }

            return app;
        }
    }
}
