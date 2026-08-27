using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Company dropdown component using SelectComponentBase.
/// </summary>
public partial class CompanySelect : SelectComponentBase<CompanyInfo>
{
    protected override string DefaultText => "Select Company";
    protected override string EntityName => "companies";
    protected override int GetKey(CompanyInfo item) => item.COID;
    protected override string GetText(CompanyInfo item) => item.COCode;

    protected override async Task<IEnumerable<CompanyInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<CompanyList>();
        return await portal.FetchAsync("CODeleted = 0 ORDER BY CODesc");
    }
}
