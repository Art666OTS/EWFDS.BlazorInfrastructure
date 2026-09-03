using System.Reflection;

namespace EWFDS.BlazorInfrastructure.Services.Theming
{
    /// <summary>
    /// Provides the installed Telerik UI for Blazor version as the single source of truth
    /// for building CDN URLs (theme stylesheets, scripts). Derived from the referenced
    /// Telerik assembly so it automatically tracks the NuGet package version instead of
    /// being hard-coded in multiple places.
    /// </summary>
    public static class TelerikVersionProvider
    {
        /// <summary>
        /// The Telerik version in "Major.Minor.Build" form (e.g. "15.0.0"),
        /// matching the CDN path segment used by blazor.cdn.telerik.com.
        /// </summary>
        public static string Version { get; } = ResolveVersion();

        private static string ResolveVersion()
        {
            // TelerikRootComponent lives in the Telerik.Blazor assembly.
            Version? version = typeof(Telerik.Blazor.Components.TelerikRootComponent)
                .Assembly
                .GetName()
                .Version;

            return version is null
                ? "15.0.0"
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
