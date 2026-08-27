using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Category dropdown component using SelectComponentBase.
/// </summary>
public partial class CategorySelect : SelectComponentBase<CategoryInfo>
{
    protected override string DefaultText => "Select Category";
    protected override string EntityName => "categories";
    protected override int GetKey(CategoryInfo item) => item.CAID;
    protected override string GetText(CategoryInfo item) => item.CADesc;

    protected override async Task<IEnumerable<CategoryInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<CategoryList>();
        return await portal.FetchAsync();
    }
}
