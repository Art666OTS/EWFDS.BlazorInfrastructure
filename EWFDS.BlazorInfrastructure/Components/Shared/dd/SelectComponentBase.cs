using Csla;
using Microsoft.AspNetCore.Components;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Abstract base class for dropdown select components.
/// Handles common loading, error handling, and selection logic.
/// </summary>
/// <typeparam name="TItem">The type of entity being selected</typeparam>
public abstract class SelectComponentBase<TItem> : ComponentBase
{
    #region Injected Services

    [Microsoft.AspNetCore.Components.Inject]
    protected IDataPortalFactory DataPortalFactory { get; set; } = default!;

    #endregion Injected Services

    #region Parameters

    /// <summary>
    /// The selected key value. Two-way bindable.
    /// </summary>
    [Parameter]
    public int SelectedKey { get; set; }

    /// <summary>
    /// Event callback invoked when the selected key changes.
    /// </summary>
    [Parameter]
    public EventCallback<int> SelectedKeyChanged { get; set; }

    #endregion Parameters

    #region Protected Fields (for use in derived .razor files)

    /// <summary>
    /// The list of options for the dropdown.
    /// </summary>
    protected List<SelectOption> Options { get; set; } = [];

    /// <summary>
    /// The currently selected value as a string (for TelerikDropDownList binding).
    /// </summary>
    protected string SelectedValue { get; set; } = string.Empty;

    /// <summary>
    /// The display text for the selected item (shown when SelectedKey > 0).
    /// </summary>
    protected string DisplayText { get; set; } = string.Empty;

    /// <summary>
    /// Error message to display if loading fails.
    /// </summary>
    protected string? ErrorMessage { get; set; }

    /// <summary>
    /// Indicates whether data is currently being loaded.
    /// </summary>
    protected bool IsLoading { get; set; } = true;

    #endregion Protected Fields (for use in derived .razor files)

    #region Abstract Members (must be implemented by derived classes)

    /// <summary>
    /// Gets the default text to display in the dropdown when no item is selected.
    /// </summary>
    protected abstract string DefaultText { get; }

    /// <summary>
    /// Gets a user-friendly name for error messages (e.g., "companies", "suppliers").
    /// </summary>
    protected abstract string EntityName { get; }

    /// <summary>
    /// Loads the items from the data source.
    /// </summary>
    /// <returns>The collection of items, or null if loading fails.</returns>
    protected abstract Task<IEnumerable<TItem>?> LoadItemsAsync();

    /// <summary>
    /// Gets the key value from an item.
    /// </summary>
    protected abstract int GetKey(TItem item);

    /// <summary>
    /// Gets the display text from an item.
    /// </summary>
    protected abstract string GetText(TItem item);

    #endregion Abstract Members (must be implemented by derived classes)

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    #endregion Lifecycle Methods

    #region Data Loading

    /// <summary>
    /// Loads data and populates the options list.
    /// </summary>
    protected virtual async Task LoadData()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var items = await LoadItemsAsync();

            if (items == null || !items.Any())
            {
                Options = [];
                DisplayText = string.Empty;
                return;
            }

            // Convert items to SelectOption list
            Options = items
                .Select(item => new SelectOption(GetKey(item).ToString(), GetText(item)))
                .ToList();

            // If SelectedKey > 0, find and set the display text
            if (SelectedKey > 0)
            {
                var match = items.FirstOrDefault(item => GetKey(item) == SelectedKey);
                DisplayText = match != null ? GetText(match) : string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading {EntityName}: {ex.Message}";
            Options = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion Data Loading

    #region Event Handlers

    /// <summary>
    /// Handles value changes from the dropdown.
    /// </summary>
    protected async Task OnValueChanged(string val)
    {
        SelectedValue = val;

        if (int.TryParse(val, out int key))
        {
            // Set DisplayText so it shows when SelectedKey > 0
            var match = Options.FirstOrDefault(o => o.Value == val);
            DisplayText = match?.Text ?? string.Empty;

            await SelectedKeyChanged.InvokeAsync(key);
        }
        else
        {
            DisplayText = string.Empty;
            await SelectedKeyChanged.InvokeAsync(0);
        }
    }

    #endregion Event Handlers

    #region Models

    /// <summary>
    /// Dropdown option record for TelerikDropDownList binding.
    /// </summary>
    protected sealed record SelectOption(string Value, string Text);

    #endregion Models
}
