import 'package:capitalism_app/features/buildings/buy_building_models.dart';
import 'package:capitalism_app/features/buildings/buy_building_service.dart';

class FakeBuyBuildingService implements BuyBuildingService {
  FakeBuyBuildingService({
    this.cities = const [],
    this.lotsByCity = const {},
    this.citiesError,
    this.purchaseError,
  });

  final List<Map<String, String>> cities;
  final Map<String, List<CityLot>> lotsByCity;
  final Object? citiesError;
  final Object? purchaseError;

  final List<String> calls = [];
  Map<String, dynamic>? lastPurchaseArgs;

  @override
  Future<List<Map<String, String>>> fetchCities() async {
    calls.add('fetchCities');
    if (citiesError != null) throw citiesError!;
    return cities;
  }

  @override
  Future<List<CityLot>> fetchLots(String cityId) async {
    calls.add('fetchLots');
    return lotsByCity[cityId] ?? const [];
  }

  @override
  Future<List<Map<String, String>>> fetchMyCompanies() async {
    calls.add('fetchMyCompanies');
    return const [];
  }

  @override
  Future<void> purchaseLot({
    required String companyId,
    required String lotId,
    required String buildingType,
    String? buildingName,
  }) async {
    calls.add('purchaseLot');
    if (purchaseError != null) throw purchaseError!;
    lastPurchaseArgs = {'companyId': companyId, 'lotId': lotId, 'buildingType': buildingType, 'buildingName': buildingName};
  }
}
