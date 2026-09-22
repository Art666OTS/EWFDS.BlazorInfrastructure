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
using Microsoft.AspNetCore.Mvc;
using EWFDS.Common.FileStorage;
using EWFDS.Common.Infrastructure;
using EWFDS.Infrastructure.Common.FileSystem;
using Microsoft.AspNetCore.SignalR;
using System.Reflection;

namespace EWFDS.Infrastructure.Common.Hosting
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
            // When enabled (API hosts), route each controller's events - and anything it calls -
            // to a per-controller folder instead of the flat by-level files. Off by default so
            // Blazor and other hosts keep the flat layout.
            bool perControllerLogging = serilogSection.GetValue<bool?>("PerControllerLogging") ?? false;

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

            if (perControllerLogging)
            {
                // Per-controller file sinks (reflection-discovered) so new controllers are picked
                // up automatically. Events are routed by the "Controller" property pushed onto the
                // LogContext by ControllerLogContextMiddleware; events with no such property (startup,
                // host, background work) fall through to the General bucket.
                AddPerControllerSinks(loggerConfig, logsDirPath, fileSizeLimitBytes, retainedFileCountLimit);
            }
            else
            {
                // File sinks (non-overlapping by level using predicate filters)
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
            }

            // Also write to console so Azure Log Stream and similar capture startup messages even
            // when file sinks fail. Console is useful for diagnostics during staging/production.
            loggerConfig = loggerConfig.WriteTo.Console();

            Log.Logger = loggerConfig.CreateLogger();

            // Replace default logging providers with Serilog and register Serilog's hosting
            // services (ILogger plus IDiagnosticContext/DiagnosticContext). Using Services.AddSerilog
            // (rather than Logging.AddSerilog) ensures DiagnosticContext is available in DI, which is
            // required by the UseSerilogRequestLogging middleware.
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog(Log.Logger, dispose: true);

            return logsDirPath;
        }

        private const string PerControllerOutputTemplate =
            "{Timestamp:o} [{Level:u3}] ({SourceContext}) {Message}{NewLine}{Exception}";

        /// <summary>
        /// Adds per-controller file sinks under <paramref name="logsDirPath"/>. For every controller
        /// discovered by reflection a folder is created with one file per level bucket
        /// (Information / Warning / Error+Fatal). Events with no "Controller" property fall through
        /// to a "General" folder. Sinks are async, roll daily and on size limit, and use shared: true.
        /// </summary>
        private static void AddPerControllerSinks(LoggerConfiguration loggerConfig, string logsDirPath, long fileSizeLimitBytes, int retainedFileCountLimit)
        {
            foreach (string controller in DiscoverControllerNames())
            {
                var c = controller;
                AddControllerLevelSink(loggerConfig, e => ControllerEquals(e, c) && e.Level == LogEventLevel.Information, Path.Combine(logsDirPath, c, "info_.log"), fileSizeLimitBytes, retainedFileCountLimit);
                AddControllerLevelSink(loggerConfig, e => ControllerEquals(e, c) && e.Level == LogEventLevel.Warning, Path.Combine(logsDirPath, c, "warning_.log"), fileSizeLimitBytes, retainedFileCountLimit);
                AddControllerLevelSink(loggerConfig, e => ControllerEquals(e, c) && e.Level >= LogEventLevel.Error, Path.Combine(logsDirPath, c, "error_.log"), fileSizeLimitBytes, retainedFileCountLimit);
            }

            // Events not tagged with a controller (startup, host, background work) go to General.
            AddControllerLevelSink(loggerConfig, e => HasNoController(e) && e.Level == LogEventLevel.Information, Path.Combine(logsDirPath, "General", "info_.log"), fileSizeLimitBytes, retainedFileCountLimit);
            AddControllerLevelSink(loggerConfig, e => HasNoController(e) && e.Level == LogEventLevel.Warning, Path.Combine(logsDirPath, "General", "warning_.log"), fileSizeLimitBytes, retainedFileCountLimit);
            AddControllerLevelSink(loggerConfig, e => HasNoController(e) && e.Level >= LogEventLevel.Error, Path.Combine(logsDirPath, "General", "error_.log"), fileSizeLimitBytes, retainedFileCountLimit);
        }

        private static void AddControllerLevelSink(LoggerConfiguration loggerConfig, Func<LogEvent, bool> filter, string path, long fileSizeLimitBytes, int retainedFileCountLimit)
        {
            loggerConfig.WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(filter)
                .WriteTo.Async(w => w.File(
                    path: path,
                    outputTemplate: PerControllerOutputTemplate,
                    rollingInterval: Serilog.RollingInterval.Day,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: retainedFileCountLimit,
                    shared: true)));
        }

        private static bool ControllerEquals(LogEvent e, string controller) =>
            e.Properties.TryGetValue("Controller", out var value)
            && value is ScalarValue scalar
            && scalar.Value is string name
            && string.Equals(name, controller, StringComparison.OrdinalIgnoreCase);

        private static bool HasNoController(LogEvent e) => !e.Properties.ContainsKey("Controller");

        /// <summary>
        /// Discovers controller names from the entry (host) assembly. Uses GetEntryAssembly so the
        /// running application's controllers are found even though this code lives in a shared library
        /// (GetExecutingAssembly would return the library and find none).
        /// </summary>
        private static IEnumerable<string> DiscoverControllerNames()
        {
            var entry = Assembly.GetEntryAssembly();
            if (entry is null)
            {
                return Array.Empty<string>();
            }

            Type?[] types;
            try
            {
                types = entry.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            return types
                .Where(t => t is not null && typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t!.Name.EndsWith("Controller", StringComparison.Ordinal)
                    ? t!.Name[..^"Controller".Length]
                    : t!.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Run all startup validation checks (common + Blazor) and map health endpoints.
        /// Throws if a fatal configuration or connectivity issue is detected.
        /// <para>
        /// This is a convenience aggregate that runs <see cref="ValidateBlazorInfrastructure"/>
        /// (Blazor/SignalR-only checks) followed by <see cref="ValidateCoreInfrastructure"/>
        /// (API-safe checks, health endpoints and readiness logging). Pure Web API hosts should
        /// call <see cref="ValidateCoreInfrastructure"/> directly instead of this method.
        /// </para>
        /// </summary>
        public static WebApplication ValidateInfrastructure(this WebApplication app, string logsDirPath)
        {
            // Run Blazor-only checks first so the "started and healthy" readiness message emitted
            // at the end of the core validation is only logged once every check has passed.
            app.ValidateBlazorInfrastructure();
            app.ValidateCoreInfrastructure(logsDirPath);
            return app;
        }

        /// <summary>
        /// Blazor-only startup validation. These checks depend on interactive Blazor/SignalR
        /// rendering and must not be run by pure Web API hosts. Throws if a fatal configuration
        /// issue is detected.
        /// </summary>
        public static WebApplication ValidateBlazorInfrastructure(this WebApplication app)
        {
            // Validate SignalR
            var hubOptions = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.SignalR.HubOptions>>();
            if (hubOptions.Value.MaximumReceiveMessageSize == null || hubOptions.Value.MaximumReceiveMessageSize <= 32 * 1024)
            {
                var msg = "SignalR MaximumReceiveMessageSize must be configured for file uploads. Default is 32KB which causes silent hangs. Add: builder.Services.Configure<HubOptions>(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024);";
                Log.Fatal(msg);
                throw new InvalidOperationException(msg);
            }

            return app;
        }
    }
}
