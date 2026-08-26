using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDS.BlazorInfrastructure.Services.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EWFDS.BlazorInfrastructure.Services.Authorization
{
    /// <summary>
    /// Base layout component class that provides authentication functionality.
    /// Inherit from this class for layout components that need user state management.
    /// Unlike ComponentBaseWithAuth, this does NOT redirect to login - it lets the page handle auth.
    /// </summary>
    public class LayoutComponentBaseWithAuth : LayoutComponentBase, IDisposable
    {
        [CascadingParameter]
        protected Task<AuthenticationState>? AuthenticationStateTask { get; set; }

        [Inject]
        protected IUserStateService UserStateService { get; set; } = default!;

        [Inject]
        protected IApplicationUserIdentity ApplicationUserIdentity { get; set; } = default!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        /// <summary>
        /// Gets the current authenticated user. May be null if not authenticated.
        /// </summary>
        protected IApplicationUserIdentity? CurrentUser { get; private set; }

        /// <summary>
        /// Cancellation token source for the component lifecycle.
        /// Use this to cancel async operations when the component is disposed.
        /// </summary>
        protected CancellationTokenSource ComponentCancellationTokenSource { get; private set; } = new();

        protected override async Task OnInitializedAsync()
        {
            // Check if user state is already loaded
            if (UserStateService.IsInitialized)
            {
                CurrentUser = UserStateService.CurrentUser;
            }
            else
            {
                // Load from authentication state
                var authState = await AuthenticationStateTask!;
                var user = authState.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    var actIdClaim = user.FindFirst("ACT_ID");
                    if (actIdClaim != null && int.TryParse(actIdClaim.Value, out int activityId))
                    {
                        CurrentUser = ApplicationUserIdentity.RePopulateAUI(activityId);
                        UserStateService.CurrentUser = CurrentUser;
                    }
                    else
                    {
                        // User is authenticated but missing ACT_ID - set to null
                        // Layout components should not redirect, let the page handle it
                        CurrentUser = null;
                    }
                }
                else
                {
                    // User is not authenticated - set to null
                    // Layout components should not redirect, let the page handle it
                    CurrentUser = null;
                }
            }
            // Subscribe to user changes
            UserStateService.OnUserChanged += OnUserChanged;
        }

        /// <summary>
        /// Called when the user state changes. Override to add custom behavior.
        /// </summary>
        protected virtual void OnUserChanged(IApplicationUserIdentity? user)
        {
            CurrentUser = user;
            InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            UserStateService.OnUserChanged -= OnUserChanged;
            ComponentCancellationTokenSource?.Cancel();
            ComponentCancellationTokenSource?.Dispose();
        }
    }
}
