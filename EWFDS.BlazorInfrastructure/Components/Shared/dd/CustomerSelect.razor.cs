using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Customer dropdown component using SelectComponentBase.
/// </summary>
public partial class CustomerSelect : SelectComponentBase<CUSTOMERInfo>
{
    protected override string DefaultText => "Select Customer";
    protected override string EntityName => "customers";
    protected override int GetKey(CUSTOMERInfo item) => item.CustID;
    protected override string GetText(CUSTOMERInfo item) => item.CustFullName;

    protected override async Task<IEnumerable<CUSTOMERInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<CUSTOMERList>();
        return await portal.FetchAsync("CustDeleted = 0 ORDER BY CustFullName");
    }
}
