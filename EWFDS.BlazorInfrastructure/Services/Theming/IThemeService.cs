namespace EWFDS.BlazorInfrastructure.Services.Theming
{
    /// <summary>
    /// Service for managing application theme switching.
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Gets the list of available themes.
        /// </summary>
        List<ThemeOption> AvailableThemes { get; }

        /// <summary>
        /// Gets the currently selected theme.
        /// </summary>
        string CurrentTheme { get; }

        /// <summary>
        /// Event raised when the theme changes.
        /// </summary>
        event EventHandler<string>? ThemeChanged;

        /// <summary>
        /// Sets the application theme.
        /// </summary>
        /// <param name="themeName">The name of the theme to apply.</param>
        Task SetThemeAsync(string themeName);

        /// <summary>
        /// Gets the current theme from local storage.
        /// </summary>
        Task<string> GetCurrentThemeAsync();

        /// <summary>
        /// Initializes the theme service by loading the saved theme.
        /// </summary>
        Task InitializeAsync();
    }

    /// <summary>
    /// Represents a theme option.
    /// </summary>
    public class ThemeOption
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
