using Csla;
using EWFDSBL8BusinessLibrary;
using EWFDSBL8.Library.PickPack.Services;
using Microsoft.AspNetCore.Components;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Reusable Location dropdown component.
/// Receives LID as integer parameter and returns selected LID to parent.
/// When LID > 0, displays the LCode as read-only text instead of the dropdown.
/// </summary>
public partial class LocationSelect : ComponentBase
{
    #region Parameters

    /// <summary>
    /// The selected Location ID. Two-way bindable.
    /// </summary>
    [Parameter]
    public int LID { get; set; }

    /// <summary>
    /// Event callback invoked when the selected LID changes.
    /// </summary>
    [Parameter]
    public EventCallback<int> LIDChanged { get; set; }

    #endregion Parameters

    #region Injected Services

    [Microsoft.AspNetCore.Components.Inject]
    private IBinLocFormService BinLocFormService { get; set; } = default!;

    [Microsoft.AspNetCore.Components.Inject]
    private IDataPortalFactory DataPortalFactory { get; set; } = default!;

    #endregion Injected Services

    #region Private Fields

    private readonly List<SelectOption> locationOptions = [];
    private string selectedValue = string.Empty;
    private string locationCode = string.Empty;
    private string errorMessage = string.Empty;
    private bool isLoading;
    private bool hasLoaded;

    #endregion Private Fields

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        isLoading = true;
        errorMessage = string.Empty;

        try
        {
            if (LID > 0)
            {
                await LoadLocationCode();
            }
            else
            {
                await LoadLocations();
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading locations: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    protected override void OnParametersSet()
    {
        // Sync the string value with the integer LID parameter
        selectedValue = LID > 0 ? LID.ToString() : string.Empty;
    }

    #endregion Lifecycle Methods

    #region Data Loading

    /// <summary>
    /// Loads the LCode for an existing location using DataPortal.
    /// </summary>
    private async Task LoadLocationCode()
    {
        var locationInfo = await DataPortalFactory.GetPortal<LocationInfo>().FetchAsync(LID);

        if (locationInfo is null)
        {
            errorMessage = $"Location with ID {LID} not found";
            locationCode = string.Empty;
            return;
        }

        locationCode = locationInfo.LCode ?? string.Empty;
    }

    /// <summary>
    /// Loads location dropdown options from the service.
    /// </summary>
    private async Task LoadLocations()
    {
        if (hasLoaded) return;

        hasLoaded = true;
        var locations = await BinLocFormService.GetLocationsAsync();

        if (locations is null)
        {
            errorMessage = "No locations available";
            return;
        }

        locationOptions.Clear();
        foreach (var loc in locations)
        {
            locationOptions.Add(new SelectOption(loc.Value, loc.Text));
        }
    }

    #endregion Data Loading

    #region Event Handlers

    private async Task OnValueChanged(string val)
    {
        selectedValue = val;

        // Parse the string value to integer and notify parent
        if (int.TryParse(val, out int lid))
        {
            // Set locationCode so it displays when LID > 0
            var match = locationOptions.FirstOrDefault(l => l.Value == val);
            locationCode = match?.Text ?? string.Empty;

            await LIDChanged.InvokeAsync(lid);
        }
        else
        {
            locationCode = string.Empty;
            await LIDChanged.InvokeAsync(0);
        }
    }

    #endregion Event Handlers

    #region Models

    /// <summary>
    /// Dropdown option record.
    /// </summary>
    private sealed record SelectOption(string Value, string Text);

    #endregion Models
}
