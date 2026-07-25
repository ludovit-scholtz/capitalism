import '../../core/graphql/graphql_service.dart';
import 'building_detail_models.dart';
import 'building_grid_models.dart';

const _unitFieldsFragment = r'''
  id unitType level resourceTypeId productTypeId minPrice maxPrice gridX gridY
  purchaseSource saleVisibility budget mediaHouseBuildingId minQuality brandScope
  vendorLockCompanyId lockedCityId industryCategory lowInventoryAlertThreshold
  linkUp linkDown linkLeft linkRight linkUpLeft linkUpRight linkDownLeft linkDownRight
''';

const _pendingUnitFieldsFragment = r'''
  id unitType gridX gridY level appliesAtTick ticksRequired isChanged isReverting
  resourceTypeId productTypeId minPrice maxPrice purchaseSource saleVisibility budget
  mediaHouseBuildingId minQuality brandScope vendorLockCompanyId lockedCityId industryCategory
  linkUp linkDown linkLeft linkRight linkUpLeft linkUpRight linkDownLeft linkDownRight
''';

const _myCompaniesQuery = '''
  query BuildingDetailMyCompanies {
    myCompanies {
      id
      cash
      buildings {
        id name type level powerStatus occupancyPercent isForSale cityId
        units { $_unitFieldsFragment }
        pendingConfiguration {
          id appliesAtTick totalTicksRequired blockReason
          units { $_pendingUnitFieldsFragment }
        }
      }
    }
  }
''';

const _catalogQuery = r'''
  query BuildingDetailCatalog {
    resourceTypes { id name basePrice }
    productTypes { id name basePrice }
  }
''';

const _fxQuery = r'''
  query BuildingDetailFx {
    cities { id currencyCode }
    fxRates { baseCurrencyCode quoteCurrencyCode rate }
  }
''';

const _scheduleUpgradeMutation = r'''
  mutation ScheduleUnitUpgrade($input: ScheduleUnitUpgradeInput!) {
    scheduleUnitUpgrade(input: $input) { id level }
  }
''';

const _updatePublicSalesPriceMutation = r'''
  mutation UpdatePublicSalesPrice($input: UpdatePublicSalesPriceInput!) {
    updatePublicSalesPrice(input: $input) { id minPrice }
  }
''';

const _storeBuildingConfigurationMutation = r'''
  mutation StoreBuildingConfiguration($input: StoreBuildingConfigurationInput!) {
    storeBuildingConfiguration(input: $input) { id appliesAtTick totalTicksRequired }
  }
''';

const _cancelBuildingConfigurationMutation = r'''
  mutation CancelBuildingConfiguration($input: CancelBuildingConfigurationInput!) {
    cancelBuildingConfiguration(input: $input) { id totalTicksRequired }
  }
''';

const _unitUpgradeInfoQuery = r'''
  query UnitUpgradeInfo($unitId: UUID!) {
    unitUpgradeInfo(unitId: $unitId) {
      unitId unitType currentLevel nextLevel isMaxLevel isUpgradable
      upgradeCost upgradeTicks currentStat nextStat statLabel
      currentLaborHoursPerTick nextLaborHoursPerTick
      currentEnergyMwhPerTick nextEnergyMwhPerTick
      currentLaborCostPerTick nextLaborCostPerTick
      currentEnergyCostPerTick nextEnergyCostPerTick
      currentStorageCapacity nextStorageCapacity
    }
  }
''';

const _inventorySummariesQuery = r'''
  query BuildingUnitInventorySummaries($buildingId: UUID!) {
    buildingUnitInventorySummaries(buildingId: $buildingId) {
      buildingUnitId quantity capacity fillPercent averageQuality
      totalSourcingCost sourcingCostPerUnit lastTickInflow lastTickOutflow
    }
  }
''';

/// GraphQL calls for the Building Detail screen and its grid editor. There
/// is no dedicated per-building query server-side — the web itself loads
/// all `myCompanies.buildings` and finds the target client-side; this
/// service does the same.
class BuildingDetailService {
  const BuildingDetailService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<BuildingDetail?> fetchBuilding(String buildingId) async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = result['myCompanies'] as List<dynamic>? ?? const [];
    for (final company in companies) {
      final companyMap = company as Map<String, dynamic>;
      final buildings = companyMap['buildings'] as List<dynamic>? ?? const [];
      for (final building in buildings) {
        if ((building as Map<String, dynamic>)['id'] == buildingId) {
          return BuildingDetail.fromJson(building, companyId: companyMap['id'] as String);
        }
      }
    }
    return null;
  }

  /// The acting company's total cash across all its bank accounts (naive,
  /// unconverted sum across currencies — see `Company.GetCash` on the
  /// backend), used for `projectedCompanyCashAfterApply`.
  Future<double?> fetchCompanyCash(String companyId) async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = result['myCompanies'] as List<dynamic>? ?? const [];
    for (final company in companies) {
      final companyMap = company as Map<String, dynamic>;
      if (companyMap['id'] == companyId) {
        return (companyMap['cash'] as num?)?.toDouble();
      }
    }
    return null;
  }

  /// Returns `(resourceTypeNamesById, productTypeNamesById)`.
  Future<(Map<String, String>, Map<String, String>)> fetchCatalogNames() async {
    final catalog = await fetchCatalog();
    return (catalog.resourceNames, catalog.productNames);
  }

  /// Full catalog including base EUR prices, used by the grid editor's
  /// item pickers and the B2B suggested-price hint.
  Future<BuildingCatalog> fetchCatalog() async {
    final result = await _graphQlService.request(_catalogQuery);
    final resources = result['resourceTypes'] as List<dynamic>? ?? const [];
    final products = result['productTypes'] as List<dynamic>? ?? const [];
    return BuildingCatalog(
      resourceNames: {for (final r in resources) (r as Map<String, dynamic>)['id'] as String: r['name'] as String},
      productNames: {for (final p in products) (p as Map<String, dynamic>)['id'] as String: p['name'] as String},
      resourceBasePrices: {
        for (final r in resources) (r as Map<String, dynamic>)['id'] as String: (r['basePrice'] as num?)?.toDouble() ?? 0,
      },
      productBasePrices: {
        for (final p in products) (p as Map<String, dynamic>)['id'] as String: (p['basePrice'] as num?)?.toDouble() ?? 0,
      },
    );
  }

  /// EUR foreign-exchange rate for [cityId]'s currency (1 for EUR cities or
  /// when the city/rate can't be resolved), mirroring `cityFxRate` in
  /// `useBuildingDetail.ts`.
  Future<double> fetchCityFxRate(String? cityId) async {
    if (cityId == null) return 1;
    final result = await _graphQlService.request(_fxQuery);
    final cities = result['cities'] as List<dynamic>? ?? const [];
    String? currencyCode;
    for (final city in cities) {
      final cityMap = city as Map<String, dynamic>;
      if (cityMap['id'] == cityId) {
        currencyCode = cityMap['currencyCode'] as String?;
        break;
      }
    }
    if (currencyCode == null || currencyCode == 'EUR') return 1;
    final rates = result['fxRates'] as List<dynamic>? ?? const [];
    for (final rate in rates) {
      final rateMap = rate as Map<String, dynamic>;
      if (rateMap['baseCurrencyCode'] == 'EUR' && rateMap['quoteCurrencyCode'] == currencyCode) {
        return (rateMap['rate'] as num?)?.toDouble() ?? 1;
      }
    }
    return 1;
  }

  Future<void> scheduleUnitUpgrade(String unitId) {
    return _graphQlService.request(
      _scheduleUpgradeMutation,
      variables: {
        'input': {'unitId': unitId},
      },
    );
  }

  Future<void> updatePublicSalesPrice({required String unitId, required double newMinPrice}) {
    return _graphQlService.request(
      _updatePublicSalesPriceMutation,
      variables: {
        'input': {'unitId': unitId, 'newMinPrice': newMinPrice},
      },
    );
  }

  /// Submits the FULL desired unit list for the building — full-replace
  /// semantics, always overwriting any existing pending plan, matching the
  /// web's `storeConfiguration()`.
  Future<void> storeBuildingConfiguration({required String buildingId, required List<EditableGridUnit> units}) {
    return _graphQlService.request(
      _storeBuildingConfigurationMutation,
      variables: {
        'input': {'buildingId': buildingId, 'units': units.map((u) => u.toMutationInput()).toList()},
      },
    );
  }

  /// Cancels the pending plan by resubmitting the building's current
  /// live-active layout (see `Mutation.BuildingConfiguration.cs` — cancel is
  /// implemented as "revert to what's live", not a plan delete).
  Future<void> cancelBuildingConfiguration(String buildingId) {
    return _graphQlService.request(
      _cancelBuildingConfigurationMutation,
      variables: {
        'input': {'buildingId': buildingId},
      },
    );
  }

  Future<UnitUpgradeInfo?> fetchUnitUpgradeInfo(String unitId) async {
    final result = await _graphQlService.request(_unitUpgradeInfoQuery, variables: {'unitId': unitId});
    final info = result['unitUpgradeInfo'] as Map<String, dynamic>?;
    return info == null ? null : UnitUpgradeInfo.fromJson(info);
  }

  Future<List<BuildingUnitInventorySummary>> fetchInventorySummaries(String buildingId) async {
    final result = await _graphQlService.request(_inventorySummariesQuery, variables: {'buildingId': buildingId});
    final list = result['buildingUnitInventorySummaries'] as List<dynamic>? ?? const [];
    return list.map((e) => BuildingUnitInventorySummary.fromJson(e as Map<String, dynamic>)).toList();
  }
}
