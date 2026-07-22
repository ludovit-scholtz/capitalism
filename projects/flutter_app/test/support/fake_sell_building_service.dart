import 'package:capitalism_app/features/buildings/sell_building_models.dart';
import 'package:capitalism_app/features/buildings/sell_building_service.dart';

class FakeSellBuildingService implements SellBuildingService {
  FakeSellBuildingService({this.building, this.fetchError, this.actionError});

  final SellableBuilding? building;
  final Object? fetchError;
  final Object? actionError;

  final List<String> calls = [];
  Map<String, dynamic>? lastSetForSaleArgs;
  String? destroyedBuildingId;

  @override
  Future<SellableBuilding?> fetchBuilding(String buildingId) async {
    calls.add('fetchBuilding');
    if (fetchError != null) throw fetchError!;
    return building;
  }

  @override
  Future<void> setForSale({required String buildingId, required bool isForSale, double? askingPrice}) async {
    calls.add('setForSale');
    if (actionError != null) throw actionError!;
    lastSetForSaleArgs = {'buildingId': buildingId, 'isForSale': isForSale, 'askingPrice': askingPrice};
  }

  @override
  Future<void> destroyBuilding(String buildingId) async {
    calls.add('destroyBuilding');
    if (actionError != null) throw actionError!;
    destroyedBuildingId = buildingId;
  }
}
