using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Catalogue Names dropdown component using SelectComponentBase.
/// </summary>
public partial class CatalogueNamesSelect : SelectComponentBase<CatalogueNamesInfo>
{
    protected override string DefaultText => "Select Catalogue";
    protected override string EntityName => "catalogues";
    protected override int GetKey(CatalogueNamesInfo item) => item.CatSubKey;
    protected override string GetText(CatalogueNamesInfo item) => item.CatSubDesc;

    protected override async Task<IEnumerable<CatalogueNamesInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<CatalogueNamesList>();
        return await portal.FetchAsync();
    }
}
