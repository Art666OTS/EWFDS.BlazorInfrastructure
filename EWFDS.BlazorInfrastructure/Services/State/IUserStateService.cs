using EWFDS.BlazorInfrastructure.Services.Identity;

namespace EWFDS.BlazorInfrastructure.Services.State
{
    /// <summary>
    /// Service for managing user state across the application.
    /// Provides access to the current user and notifications when the user changes.
    /// </summary>
    public interface IUserStateService
    {
        /// <summary>
        /// Gets or sets the current authenticated user.
        /// </summary>
        IApplicationUserIdentity? CurrentUser { get; set; }

        /// <summary>
        /// Gets whether the user state has been initialized.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Event raised when the current user changes.
        /// </summary>
        event Action<IApplicationUserIdentity?>? OnUserChanged;

        /// <summary>
        /// Clears the current user state.
        /// </summary>
        void Clear();
    }
}
