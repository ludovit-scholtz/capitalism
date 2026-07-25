import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_detail_service.dart';
import 'package:capitalism_app/features/buildings/building_grid_models.dart';

class FakeBuildingDetailService implements BuildingDetailService {
  FakeBuildingDetailService({
    this.building,
    this.resourceNames = const {},
    this.productNames = const {},
    this.resourceBasePrices = const {},
    this.productBasePrices = const {},
    this.companyCash,
    this.cityFxRate = 1,
    this.unitUpgradeInfo,
    this.inventorySummaries = const [],
    this.fetchError,
    this.actionError,
    this.storeConfigurationError,
    this.cancelConfigurationError,
  });

  final BuildingDetail? building;
  final Map<String, String> resourceNames;
  final Map<String, String> productNames;
  final Map<String, double> resourceBasePrices;
  final Map<String, double> productBasePrices;
  final double? companyCash;
  final double cityFxRate;
  final UnitUpgradeInfo? unitUpgradeInfo;
  final List<BuildingUnitInventorySummary> inventorySummaries;
  final Object? fetchError;
  final Object? actionError;
  final Object? storeConfigurationError;
  final Object? cancelConfigurationError;

  final List<String> calls = [];
  final List<String> upgradedUnitIds = [];
  Map<String, dynamic>? lastPriceUpdateArgs;
  List<EditableGridUnit>? lastStoredUnits;
  String? lastStoredBuildingId;
  String? lastCancelledBuildingId;

  @override
  Future<BuildingDetail?> fetchBuilding(String buildingId) async {
    calls.add('fetchBuilding');
    if (fetchError != null) throw fetchError!;
    return building;
  }

  @override
  Future<double?> fetchCompanyCash(String companyId) async {
    calls.add('fetchCompanyCash');
    return companyCash;
  }

  @override
  Future<(Map<String, String>, Map<String, String>)> fetchCatalogNames() async {
    calls.add('fetchCatalogNames');
    return (resourceNames, productNames);
  }

  @override
  Future<BuildingCatalog> fetchCatalog() async {
    calls.add('fetchCatalog');
    return BuildingCatalog(
      resourceNames: resourceNames,
      productNames: productNames,
      resourceBasePrices: resourceBasePrices,
      productBasePrices: productBasePrices,
    );
  }

  @override
  Future<double> fetchCityFxRate(String? cityId) async {
    calls.add('fetchCityFxRate');
    return cityFxRate;
  }

  @override
  Future<void> scheduleUnitUpgrade(String unitId) async {
    calls.add('scheduleUnitUpgrade');
    if (actionError != null) throw actionError!;
    upgradedUnitIds.add(unitId);
  }

  @override
  Future<void> updatePublicSalesPrice({required String unitId, required double newMinPrice}) async {
    calls.add('updatePublicSalesPrice');
    if (actionError != null) throw actionError!;
    lastPriceUpdateArgs = {'unitId': unitId, 'newMinPrice': newMinPrice};
  }

  @override
  Future<void> storeBuildingConfiguration({required String buildingId, required List<EditableGridUnit> units}) async {
    calls.add('storeBuildingConfiguration');
    if (storeConfigurationError != null) throw storeConfigurationError!;
    lastStoredBuildingId = buildingId;
    lastStoredUnits = units;
  }

  @override
  Future<void> cancelBuildingConfiguration(String buildingId) async {
    calls.add('cancelBuildingConfiguration');
    if (cancelConfigurationError != null) throw cancelConfigurationError!;
    lastCancelledBuildingId = buildingId;
  }

  @override
  Future<UnitUpgradeInfo?> fetchUnitUpgradeInfo(String unitId) async {
    calls.add('fetchUnitUpgradeInfo');
    return unitUpgradeInfo;
  }

  @override
  Future<List<BuildingUnitInventorySummary>> fetchInventorySummaries(String buildingId) async {
    calls.add('fetchInventorySummaries');
    return inventorySummaries;
  }
}
