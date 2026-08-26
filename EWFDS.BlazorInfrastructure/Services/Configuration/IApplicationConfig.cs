namespace EWFDS.BlazorInfrastructure.Services.Configuration
{
    /// <summary>
    /// Interface for application-specific configuration values.
    /// Each app implements this to provide its own identity values.
    /// </summary>
    public interface IApplicationConfig
    {
        /// <summary>
        /// Gets the application name (e.g., "PickPack", "eWFDS").
        /// Used for activity logging and identification.
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// Gets the seed activity text (e.g., "PickPack Seed Activity").
        /// </summary>
        string SeedActivityText { get; }
    }
}
