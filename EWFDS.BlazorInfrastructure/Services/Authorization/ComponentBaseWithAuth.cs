using EWFDS.BlazorInfrastructure.Services.Identity;
using EWFDS.BlazorInfrastructure.Services.State;
using EWFDSBL8.Library.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace EWFDS.BlazorInfrastructure.Services.Authorization
{
    /// <summary>
    /// Base component class that provides authentication and authorization functionality.
    /// Inherit from this class to get automatic user state management in your components.
    /// </summary>
    public class ComponentBaseWithAuth : ComponentBase, IDisposable
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
        /// Gets the current authenticated user. Guaranteed non-null after OnInitializedAsync.
        /// </summary>
        protected IApplicationUserIdentity? CurrentUser { get; private set; }

        /// <summary>
        /// Cancellation token source for the component lifecycle.
        /// Use this to cancel async operations when the component is disposed.
        /// </summary>
        protected CancellationTokenSource ComponentCancellationTokenSource { get; private set; } = new();

        #region Common User Properties

        /// <summary>
        /// Gets the current user's key (primary identifier).
        /// Returns 0 if user is not loaded.
        /// </summary>
        protected int UserKey => CurrentUser?.Key ?? 0;

        /// <summary>
        /// Gets the current user's full name.
        /// Returns empty string if user is not loaded.
        /// </summary>
        protected string UserFullName => CurrentUser?.FullName ?? string.Empty;

        /// <summary>
        /// Gets the current user's customer ID.
        /// Returns 0 if user is not loaded or has no customer.
        /// </summary>
        protected int UserCustomerId => CurrentUser?.Customer?.CustID ?? 0;

        /// <summary>
        /// Gets the current user's customer full name.
        /// Returns empty string if user is not loaded or has no customer.
        /// </summary>
        protected string UserCustomerName => CurrentUser?.Customer?.CustFullName ?? string.Empty;

        /// <summary>
        /// Gets the current user's company ID.
        /// Returns 0 if user is not loaded.
        /// </summary>
        protected int UserCompanyId => CurrentUser?.COID ?? 0;

        /// <summary>
        /// Gets whether the current user is a WFDS staff member.
        /// Returns false if user is not loaded.
        /// </summary>
        protected bool IsWFDSStaff => CurrentUser?.WFDSStaff ?? false;

        /// <summary>
        /// Gets whether the current user is a customer user type.
        /// Returns false if user is not loaded.
        /// </summary>
        protected bool IsCustomerUser => CurrentUser?.IsUser ?? false;

        /// <summary>
        /// Gets whether the current user has only the Pick role (no other roles).
        /// Returns false if user is not loaded or has any other roles.
        /// </summary>
        protected bool IsPickRoleOnly
        {
            get
            {
                if (CurrentUser?.claims == null)
                    return false;
                var roles = CurrentUser.claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();
                return roles.Count == 1 && roles[0] == "Pick";
            }
        }

        /// <summary>
        /// Checks if the current user is in the specified role.
        /// Returns false if user is not loaded.
        /// </summary>
        protected bool IsInRole(string role) => CurrentUser?.IsInRole(role) ?? false;

        #endregion Common User Properties

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
                        NavigationManager.NavigateTo("/Account/Login", true);
                    }
                }
                else
                {
                    NavigationManager.NavigateTo("/Account/Login", true);
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

        #region User Context Helpers

        /// <summary>
        /// Gets the current user's level mapped to UserLevelType enum.
        /// Returns Customer level if user is not loaded.
        /// </summary>
        protected UserLevelType GetCurrentUserLevel()
        {
            if (CurrentUser == null)
                return UserLevelType.Customer;

            return MapUserLevel(CurrentUser.UserLevel);
        }

        /// <summary>
        /// Maps the application UserLevel integer to UserLevelType enum.
        /// Based on legacy system user level mappings.
        /// </summary>
        protected static UserLevelType MapUserLevel(int userLevel)
        {
            return userLevel switch
            {
                3 => UserLevelType.CatalogueManager,
                4 => UserLevelType.CSOAccountManager,
                5 => UserLevelType.OrderManager,
                7 => UserLevelType.ServiceProvider,
                8 => UserLevelType.OrderDirector,
                10 => UserLevelType.Customer,
                11 => UserLevelType.Picker,
                12 => UserLevelType.Packer,
                13 => UserLevelType.WarehouseWorker,
                27 => UserLevelType.AccountManager,
                28 => UserLevelType.HelpDesk,
                29 => UserLevelType.CompanyUser,
                40 => UserLevelType.WarehouseManager,
                45 => UserLevelType.CallCentre,
                60 => UserLevelType.SuperUser,
                _ => UserLevelType.Customer // Default fallback
            };
        }

        /// <summary>
        /// Builds a standard user context for service calls.
        /// Can be overridden in derived classes to add additional context.
        /// </summary>
        protected virtual OrderSearchUserContext BuildUserContext()
        {
            if (CurrentUser == null)
            {
                // Return a default restricted context if user not loaded
                return new OrderSearchUserContext(
                    UserKey: 0,
                    CustomerId: 0,
                    CompanyId: 0,
                    UserLevel: UserLevelType.Customer,
                    IsWFDSStaff: false,
                    IsUser: false,
                    AssociatedCompanies: null);
            }

            return new OrderSearchUserContext(
                UserKey: CurrentUser.Key,
                CustomerId: CurrentUser.CSOCustomerID,
                CompanyId: CurrentUser.COID,
                UserLevel: MapUserLevel(CurrentUser.UserLevel),
                IsWFDSStaff: CurrentUser.WFDSStaff,
                IsUser: CurrentUser.IsUser,
                AssociatedCompanies: null); // Associated companies loaded separately if needed
        }

        #endregion User Context Helpers

        public void Dispose()
        {
            UserStateService.OnUserChanged -= OnUserChanged;
            ComponentCancellationTokenSource?.Cancel();
            ComponentCancellationTokenSource?.Dispose();
        }
    }
}
