using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Rack dropdown component using SelectComponentBase.
/// </summary>
public partial class RackSelect : SelectComponentBase<RackInfo>
{
    protected override string DefaultText => "Select Rack";
    protected override string EntityName => "racks";
    protected override int GetKey(RackInfo item) => item.RackID;
    protected override string GetText(RackInfo item) => item.RackDesc;

    protected override async Task<IEnumerable<RackInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<RackList>();
        return await portal.FetchAsync();
    }
}
