namespace EWFDS.BlazorInfrastructure.Components.Shared.Models;

/// <summary>
/// Defines the types of status messages that can be displayed.
/// </summary>
public enum AlertType
{
    /// <summary>
    /// Success message (green).
    /// </summary>
    Success,

    /// <summary>
    /// Error message (red).
    /// </summary>
    Error,

    /// <summary>
    /// Warning message (yellow).
    /// </summary>
    Warning,

    /// <summary>
    /// Informational message (blue).
    /// </summary>
    Info
}
