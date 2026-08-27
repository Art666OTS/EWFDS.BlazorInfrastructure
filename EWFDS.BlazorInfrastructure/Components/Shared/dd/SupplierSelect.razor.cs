using EWFDSBL8BusinessLibrary;

namespace EWFDS.BlazorInfrastructure.Components.Shared.dd;

/// <summary>
/// Supplier dropdown component using SelectComponentBase.
/// </summary>
public partial class SupplierSelect : SelectComponentBase<SuppliersInfo>
{
    protected override string DefaultText => "Select Supplier";
    protected override string EntityName => "suppliers";
    protected override int GetKey(SuppliersInfo item) => item.SupplierID;
    protected override string GetText(SuppliersInfo item) => item.SupplierCode;

    protected override async Task<IEnumerable<SuppliersInfo>?> LoadItemsAsync()
    {
        var portal = DataPortalFactory.GetPortal<SuppliersList>();
        return await portal.FetchAsync();
    }
}
