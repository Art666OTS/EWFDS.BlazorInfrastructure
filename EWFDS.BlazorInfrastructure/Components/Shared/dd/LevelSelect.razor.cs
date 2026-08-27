using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Level dropdown component using SelectComponentBase.
/// </summary>
public partial class LevelSelect : SelectComponentBase<LevelInfo>
{
    protected override string DefaultText => "Select Level";
    protected override string EntityName => "levels";
    protected override int GetKey(LevelInfo item) => item.LevelID;
    protected override string GetText(LevelInfo item) => item.LevelDesc;

    protected override async Task<IEnumerable<LevelInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<LevelList>();
        return await portal.FetchAsync();
    }
}
