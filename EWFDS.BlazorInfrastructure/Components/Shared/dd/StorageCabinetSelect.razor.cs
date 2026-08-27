using EWFDSBL8.Library.PickPack.Services;
using Microsoft.AspNetCore.Components;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Dropdown component for selecting Storage Cabinet items.
/// Filters by CatActive = 10 or 15 and CatOwner = CompanyId.
/// </summary>
public partial class StorageCabinetSelect : ComponentBase
{
    [Microsoft.AspNetCore.Components.Inject]
    private IStorageCabinetService StorageCabinetService { get; set; } = default!;

    /// <summary>
    /// The selected CatKey value.
    /// </summary>
    [Parameter]
    public int CatKey { get; set; }

    /// <summary>
    /// Callback when CatKey changes.
    /// </summary>
    [Parameter]
    public EventCallback<int> CatKeyChanged { get; set; }

    /// <summary>
    /// The Company ID to filter storage cabinet items by.
    /// </summary>
    [Parameter]
    public int CompanyId { get; set; }

    private List<SelectOption> options = [];
    private string selectedValue = string.Empty;
    private string displayText = string.Empty;
    private string? errorMessage;
    private bool isLoading = true;
    private int lastLoadedCompanyId = 0;

    private record SelectOption(string Value, string Text);

    protected override async Task OnParametersSetAsync()
    {
        // Load only if CompanyId is valid and has changed
        if (CompanyId > 0 && CompanyId != lastLoadedCompanyId)
        {
            await LoadStorageCabinets();
        }
        else if (CompanyId == 0)
        {
            isLoading = false;
            errorMessage = "Company ID not available.";
        }
    }

    private async Task LoadStorageCabinets()
    {
        isLoading = true;
        errorMessage = null;
        StateHasChanged();

        try
        {
            // Build criteria: CatActive = 10 or 15 (status "10,15") and CatOwner = CompanyId
            var criteria = new StorageCabinetListCriteria(
                CatalogueStatus: "10,15",
                IsCompanyUser: true,
                CompanyId: CompanyId,
                AdditionalFilter: null,
                UseCatalogueView: true
            );

            var result = await StorageCabinetService.GetStorageCabinetListAsync(criteria);

            lastLoadedCompanyId = CompanyId;

            if (result.Items == null || result.Items.Count == 0)
            {
                options = [];
                displayText = string.Empty;
                errorMessage = "No storage cabinet items found for this company.";
                return;
            }

            // Map to SelectOption using CatID (SKU) only as display text
            options = result.Items
                .Select(x => new SelectOption(x.CatKey.ToString(), x.CatID))
                .ToList();

            // If CatKey > 0, find matching item to set display text
            if (CatKey > 0)
            {
                var match = result.Items.FirstOrDefault(x => x.CatKey == CatKey);
                displayText = match?.CatID ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading storage cabinets: {ex.Message}";
            options = [];
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task OnValueChanged(string val)
    {
        selectedValue = val;

        if (int.TryParse(val, out int catKey))
        {
            // Set displayText so it displays when CatKey > 0
            var match = options.FirstOrDefault(o => o.Value == val);
            displayText = match?.Text ?? string.Empty;

            await CatKeyChanged.InvokeAsync(catKey);
        }
        else
        {
            displayText = string.Empty;
            await CatKeyChanged.InvokeAsync(0);
        }
    }
}
