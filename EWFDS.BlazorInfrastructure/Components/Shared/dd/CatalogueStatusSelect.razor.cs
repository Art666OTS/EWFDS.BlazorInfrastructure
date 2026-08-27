using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Catalogue Status dropdown component using SelectComponentBase.
/// </summary>
public partial class CatalogueStatusSelect : SelectComponentBase<CatalogueStatusInfo>
{
    protected override string DefaultText => "Select Status";
    protected override string EntityName => "statuses";
    protected override int GetKey(CatalogueStatusInfo item) => item.CSTID;
    protected override string GetText(CatalogueStatusInfo item) => item.CSTCode;

    protected override async Task<IEnumerable<CatalogueStatusInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<CatalogueStatusList>();
        return await portal.FetchAsync();
    }
}
