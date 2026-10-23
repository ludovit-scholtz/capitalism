using Api.Data.Entities;

namespace Api.Engine;

public sealed partial class TickContext
{
    private Dictionary<(Guid CityId, Guid CompanyId), List<(Building Building, BuildingUnit Unit)>>? _b2bSellerUnits;
    private int _b2bSellerUnitsIndexedBuildings = -1;

    private Dictionary<Guid, List<ExchangeOrder>>? _sellOrdersByResource;
    private Dictionary<Guid, List<ExchangeOrder>>? _sellOrdersByProduct;
    private int _sellOrdersIndexedCount = -1;

    /// <summary>
    /// The B2B sales units, outside upgrade, of one company in one city, in the order <see cref="UnitsByBuilding"/> lists them.
    /// Every purchase unit of a tick looks for local suppliers; scanning every unit of the world for each of them made the purchasing
    /// phase quadratic (50 s per tick at 20,000 buildings, #646). Which units exist and which are under upgrade does not change during
    /// a tick; the index is rebuilt if buildings were added to or removed from <see cref="UnitsByBuilding"/>.
    /// </summary>
    public IReadOnlyList<(Building Building, BuildingUnit Unit)> GetB2BSellerUnits(Guid cityId, Guid companyId)
    {
        if (_b2bSellerUnits is null || _b2bSellerUnitsIndexedBuildings != UnitsByBuilding.Count)
        {
            var index = new Dictionary<(Guid CityId, Guid CompanyId), List<(Building Building, BuildingUnit Unit)>>();
            foreach (var (buildingId, units) in UnitsByBuilding)
            {
                if (!BuildingsById.TryGetValue(buildingId, out var building))
                {
                    continue;
                }

                foreach (var unit in units)
                {
                    if (unit.UnitType != UnitType.B2BSales || UnitsUnderUpgrade.Contains(unit.Id))
                    {
                        continue;
                    }

                    var key = (building.CityId, building.CompanyId);
                    if (!index.TryGetValue(key, out var list))
                    {
                        index[key] = list = [];
                    }

                    list.Add((building, unit));
                }
            }

            _b2bSellerUnits = index;
            _b2bSellerUnitsIndexedBuildings = UnitsByBuilding.Count;
        }

        return _b2bSellerUnits.TryGetValue((cityId, companyId), out var sellers) ? sellers : [];
    }

    /// <summary>
    /// The SELL orders that could match a purchase of this resource and/or product, in the order of <see cref="ActiveExchangeOrders"/>.
    /// Whether an order is still active, or has quantity left, changes while purchases fill it, so callers check that themselves.
    /// </summary>
    public IReadOnlyList<ExchangeOrder> GetSellOrderCandidates(Guid? resourceTypeId, Guid? productTypeId)
    {
        if (_sellOrdersByResource is null || _sellOrdersIndexedCount != ActiveExchangeOrders.Count)
        {
            _sellOrdersByResource = [];
            _sellOrdersByProduct = [];
            foreach (var order in ActiveExchangeOrders)
            {
                if (order.Side != "SELL")
                {
                    continue;
                }

                if (order.ResourceTypeId.HasValue)
                {
                    AddTo(_sellOrdersByResource, order.ResourceTypeId.Value, order);
                }

                if (order.ProductTypeId.HasValue)
                {
                    AddTo(_sellOrdersByProduct, order.ProductTypeId.Value, order);
                }
            }

            _sellOrdersIndexedCount = ActiveExchangeOrders.Count;
        }

        // A purchase needs an item; with both set an order must match both, so either index holds every candidate.
        if (resourceTypeId.HasValue)
        {
            return _sellOrdersByResource.TryGetValue(resourceTypeId.Value, out var byResource) ? byResource : [];
        }

        if (productTypeId.HasValue)
        {
            return _sellOrdersByProduct!.TryGetValue(productTypeId.Value, out var byProduct) ? byProduct : [];
        }

        return ActiveExchangeOrders.Where(order => order.Side == "SELL").ToList();

        static void AddTo(Dictionary<Guid, List<ExchangeOrder>> index, Guid key, ExchangeOrder order)
        {
            if (!index.TryGetValue(key, out var list))
            {
                index[key] = list = [];
            }

            list.Add(order);
        }
    }
}
