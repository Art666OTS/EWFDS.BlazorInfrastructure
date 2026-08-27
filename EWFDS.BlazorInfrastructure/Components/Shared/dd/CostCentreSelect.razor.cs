using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Cost Centre dropdown component using SelectComponentBase.
/// </summary>
public partial class CostCentreSelect : SelectComponentBase<CostCentreInfo>
{
    protected override string DefaultText => "Select Cost Centre";
    protected override string EntityName => "cost centres";
    protected override int GetKey(CostCentreInfo item) => item.CostKey;
    protected override string GetText(CostCentreInfo item) => item.CostID;

    protected override async Task<IEnumerable<CostCentreInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<CostCentreList>();
        return await portal.FetchAsync("1 = 1 ORDER BY CostID");
    }
}
