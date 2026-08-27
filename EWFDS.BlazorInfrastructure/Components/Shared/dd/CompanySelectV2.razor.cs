using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Company dropdown component using the generic SelectComponentBase.
/// Demonstrates the simplified pattern for dropdown components.
/// </summary>
public partial class CompanySelectV2 : SelectComponentBase<CompanyInfo>
{
    #region Abstract Member Implementations

    protected override string DefaultText => "Select Company";

    protected override string EntityName => "companies";

    protected override int GetKey(CompanyInfo item) => item.COID;

    protected override string GetText(CompanyInfo item) => item.COCode;

    protected override async Task<IEnumerable<CompanyInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<CompanyList>();
        return await portal.FetchAsync("CODeleted = 0 ORDER BY CODesc");
    }

    #endregion Abstract Member Implementations
}
