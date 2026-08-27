using EWFDS.BlazorInfrastructure.Components.Shared.dd;
using EWFDSBL8.Library.PickPack.Services;
using Microsoft.AspNetCore.Components;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Reusable Bin Type dropdown component.
/// Receives BTID as integer parameter and returns selected BTID to parent.
/// When BTID > 0, displays the BTDesc as read-only text instead of the dropdown.
/// </summary>
public partial class BinTypeSelect : ComponentBase
{
    #region Parameters

    /// <summary>
    /// The selected Bin Type ID. Two-way bindable.
    /// </summary>
    [Parameter]
    public int BTID { get; set; }

    /// <summary>
    /// Event callback invoked when the selected BTID changes.
    /// </summary>
    [Parameter]
    public EventCallback<int> BTIDChanged { get; set; }

    #endregion Parameters

    #region Injected Services

    [Microsoft.AspNetCore.Components.Inject]
    private IBinLocFormService BinLocFormService { get; set; } = default!;

    #endregion Injected Services

    #region Private Fields

    private readonly List<SelectOption> binTypeOptions = [];
    private string selectedValue = string.Empty;
    private string binTypeDesc = string.Empty;
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
            await LoadBinTypes();

            // If BTID is set, find the description from loaded options
            if (BTID > 0)
            {
                var match = binTypeOptions.FirstOrDefault(x => x.Value == BTID.ToString());
                binTypeDesc = match?.Text ?? $"Unknown ({BTID})";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading bin types: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    protected override void OnParametersSet()
    {
        // Sync the string value with the integer BTID parameter
        selectedValue = BTID > 0 ? BTID.ToString() : string.Empty;
    }

    #endregion Lifecycle Methods

    #region Data Loading

    /// <summary>
    /// Loads bin type dropdown options from the service.
    /// </summary>
    private async Task LoadBinTypes()
    {
        if (hasLoaded) return;

        hasLoaded = true;
        var binTypes = await BinLocFormService.GetBinTypesAsync();

        if (binTypes is null)
        {
            errorMessage = "No bin types available";
            return;
        }

        binTypeOptions.Clear();
        foreach (var bt in binTypes)
        {
            binTypeOptions.Add(new SelectOption(bt.Value, bt.Text));
        }
    }

    #endregion Data Loading

    #region Event Handlers

    private async Task OnValueChanged(string val)
    {
        selectedValue = val;

        // Parse the string value to integer and notify parent
        if (int.TryParse(val, out int btid))
        {
            // Set binTypeDesc so it displays when BTID > 0
            var match = binTypeOptions.FirstOrDefault(b => b.Value == val);
            binTypeDesc = match?.Text ?? string.Empty;

            await BTIDChanged.InvokeAsync(btid);
        }
        else
        {
            binTypeDesc = string.Empty;
            await BTIDChanged.InvokeAsync(0);
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
