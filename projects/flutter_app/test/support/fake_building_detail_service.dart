import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_detail_service.dart';

class FakeBuildingDetailService implements BuildingDetailService {
  FakeBuildingDetailService({
    this.building,
    this.resourceNames = const {},
    this.productNames = const {},
    this.fetchError,
    this.actionError,
  });

  final BuildingDetail? building;
  final Map<String, String> resourceNames;
  final Map<String, String> productNames;
  final Object? fetchError;
  final Object? actionError;

  final List<String> calls = [];
  final List<String> upgradedUnitIds = [];
  Map<String, dynamic>? lastPriceUpdateArgs;

  @override
  Future<BuildingDetail?> fetchBuilding(String buildingId) async {
    calls.add('fetchBuilding');
    if (fetchError != null) throw fetchError!;
    return building;
  }

  @override
  Future<(Map<String, String>, Map<String, String>)> fetchCatalogNames() async {
    calls.add('fetchCatalogNames');
    return (resourceNames, productNames);
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
}
