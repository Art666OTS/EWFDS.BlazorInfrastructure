using EWFDS.BlazorInfrastructure.Services.Identity;

namespace EWFDS.BlazorInfrastructure.Services.State
{
    /// <summary>
    /// Service for managing user state across the application.
    /// Implements scoped state management for the current authenticated user.
    /// </summary>
    public class UserStateService : IUserStateService
    {
        private IApplicationUserIdentity? _currentUser;

        /// <summary>
        /// Gets or sets the current authenticated user.
        /// Setting this property will trigger the OnUserChanged event.
        /// </summary>
        public IApplicationUserIdentity? CurrentUser
        {
            get => _currentUser;
            set
            {
                _currentUser = value;
                OnUserChanged?.Invoke(_currentUser);
            }
        }

        /// <summary>
        /// Gets whether the user state has been initialized (user is not null).
        /// </summary>
        public bool IsInitialized => _currentUser != null;

        /// <summary>
        /// Event raised when the current user changes.
        /// Subscribe to this event to be notified of user state changes.
        /// </summary>
        public event Action<IApplicationUserIdentity?>? OnUserChanged;

        /// <summary>
        /// Clears the current user state.
        /// This will set CurrentUser to null and trigger the OnUserChanged event.
        /// </summary>
        public void Clear()
        {
            CurrentUser = null;
        }
    }
}
