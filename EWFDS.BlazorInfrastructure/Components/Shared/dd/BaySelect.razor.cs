using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Bay dropdown component using SelectComponentBase.
/// </summary>
public partial class BaySelect : SelectComponentBase<BayInfo>
{
    protected override string DefaultText => "Select Bay";
    protected override string EntityName => "bays";
    protected override int GetKey(BayInfo item) => item.BayID;
    protected override string GetText(BayInfo item) => item.BayDesc;

    protected override async Task<IEnumerable<BayInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<BayList>();
        return await portal.FetchAsync();
    }
}
