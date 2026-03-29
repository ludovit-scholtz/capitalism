namespace Api.Types;

public sealed class BuildingUnitInventory
{
    public Guid Id { get; set; }
    public Guid BuildingUnitId { get; set; }
    public Guid? ResourceTypeId { get; set; }
    public Guid? ProductTypeId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Quality { get; set; }
}