using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Carrier dropdown component using SelectComponentBase.
/// </summary>
public partial class CarrierSelect : SelectComponentBase<tblCourierInfo>
{
    protected override string DefaultText => "Select Carrier";
    protected override string EntityName => "carriers";
    protected override int GetKey(tblCourierInfo item) => item.CourierIDPK;
    protected override string GetText(tblCourierInfo item) => item.CourierName;

    protected override async Task<IEnumerable<tblCourierInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<tblCourierList>();
        return await portal.FetchAsync("CourierDeleted = 0 AND CourierType = 2 ORDER BY CourierName");
    }
}
