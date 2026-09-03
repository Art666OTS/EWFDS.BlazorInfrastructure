using Microsoft.JSInterop;

namespace EWFDS.BlazorInfrastructure.Services.Theming
{
    /// <summary>
    /// Service for managing application theme switching with support for Telerik Kendo themes.
    /// </summary>
    public class ThemeService : IThemeService, IAsyncDisposable
    {
        private const string STORAGE_KEY = "app-theme";
        private const string DEFAULT_THEME = "default-ocean-blue";
        private const string TELERIK_VERSION = "14.0.0";

        // For Razor Class Libraries, JS files are served from _content/{AssemblyName}/
        private const string JS_MODULE_PATH = "./_content/EWFDS.BlazorInfrastructure/js/themeService.js";

        private readonly IJSRuntime _jsRuntime;
        private IJSObjectReference? _module;
        private string _currentTheme = DEFAULT_THEME;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public event EventHandler<string>? ThemeChanged;

        public ThemeService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            InitializeThemes();
        }

        public List<ThemeOption> AvailableThemes { get; private set; } = new();

        public string CurrentTheme => _currentTheme;

        private void InitializeThemes()
        {
            AvailableThemes = new List<ThemeOption>
            {
                new ThemeOption
                {
                    Name = "default-main",
                    DisplayName = "Default Main",
                    Url = $"https://blazor.cdn.telerik.com/blazor/{TELERIK_VERSION}/kendo-theme-default/swatches/default-main.css",
                    Description = "Classic default theme with main colors"
                },
                new ThemeOption
                {
                    Name = "default-ocean-blue",
                    DisplayName = "Ocean Blue",
                    Url = $"https://blazor.cdn.telerik.com/blazor/{TELERIK_VERSION}/kendo-theme-default/swatches/default-ocean-blue.css",
                    Description = "Default theme with ocean blue accent"
                },
                new ThemeOption
                {
                    Name = "default-purple",
                    DisplayName = "Purple",
                    Url = $"https://blazor.cdn.telerik.com/blazor/{TELERIK_VERSION}/kendo-theme-default/swatches/default-purple.css",
                    Description = "Default theme with purple accent"
                },
                new ThemeOption
                {
                    Name = "default-nordic",
                    DisplayName = "Nordic",
                    Url = $"https://blazor.cdn.telerik.com/blazor/{TELERIK_VERSION}/kendo-theme-default/swatches/default-nordic.css",
                    Description = "Default theme with nordic colors"
                },
                new ThemeOption
                {
                    Name = "default-turquoise",
                    DisplayName = "Turquoise",
                    Url = $"https://blazor.cdn.telerik.com/blazor/{TELERIK_VERSION}/kendo-theme-default/swatches/default-turquoise.css",
                    Description = "Default theme with turquoise accent"
                },
                new ThemeOption
                {
                    Name = "bootstrap-main",
                    DisplayName = "Bootstrap",
                    Url = $"https://blazor.cdn.telerik.com/blazor/{TELERIK_VERSION}/kendo-theme-bootstrap/swatches/bootstrap-main.css",
                    Description = "Bootstrap-inspired theme"
                },
                new ThemeOption
                {
                    Name = "material-main",
                    DisplayName = "Material",
                    Url = $"https://blazor.cdn.telerik.com/blazor/{TELERIK_VERSION}/kendo-theme-material/swatches/material-main.css",
                    Description = "Google Material Design theme"
                },
                new ThemeOption
                {
                    Name = "fluent-main",
                    DisplayName = "Fluent",
                    Url = $"https://blazor.cdn.telerik.com/blazor/{TELERIK_VERSION}/kendo-theme-fluent/swatches/fluent-main.css",
                    Description = "Microsoft Fluent Design theme"
                }
            };
        }

        public async Task InitializeAsync()
        {
            // Prevent multiple initializations
            if (_isInitialized)
            {
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return; // Double-check after acquiring lock

                // Load JavaScript module for theme switching
                _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", JS_MODULE_PATH);

                var savedTheme = await GetCurrentThemeAsync();
                if (!string.IsNullOrEmpty(savedTheme))
                {
                    _currentTheme = savedTheme;
                    // Apply the saved theme on initialization
                    var theme = AvailableThemes.FirstOrDefault(t => t.Name == savedTheme);
                    if (theme != null && _module != null)
                    {
                        await _module.InvokeVoidAsync("setTheme", theme.Url, savedTheme);
                    }
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing theme service: {ex.Message}");
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task SetThemeAsync(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                themeName = DEFAULT_THEME;
            }

            var theme = AvailableThemes.FirstOrDefault(t => t.Name == themeName);
            if (theme == null)
            {
                themeName = DEFAULT_THEME;
                theme = AvailableThemes.First(t => t.Name == DEFAULT_THEME);
            }

            try
            {
                // Apply theme via JavaScript
                if (_module != null)
                {
                    await _module.InvokeVoidAsync("setTheme", theme.Url, themeName);
                }

                _currentTheme = themeName;

                // Store in localStorage
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", STORAGE_KEY, themeName);

                // Notify subscribers
                ThemeChanged?.Invoke(this, themeName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting theme: {ex.Message}");
            }
        }

        public async Task<string> GetCurrentThemeAsync()
        {
            try
            {
                var theme = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", STORAGE_KEY);
                return theme ?? DEFAULT_THEME;
            }
            catch
            {
                return DEFAULT_THEME;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_module != null)
            {
                try
                {
                    await _module.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                    // Expected when the circuit has already disconnected (e.g. on page reload).
                }
                catch
                {
                    // Ignore any other disposal errors
                }
            }

            _initLock.Dispose();
        }
    }
}
