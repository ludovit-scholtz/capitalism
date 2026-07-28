import 'package:capitalism_app/features/buildings/buy_building_models.dart';
import 'package:capitalism_app/features/buildings/buy_building_service.dart';

class FakeBuyBuildingService implements BuyBuildingService {
  FakeBuyBuildingService({
    this.cities = const [],
    this.lotsByCity = const {},
    this.myBuildingLocations = const [],
    this.citiesError,
    this.purchaseError,
    this.purchasedBuildingId = 'building-new',
  });

  final List<BuyBuildingCity> cities;
  final Map<String, List<CityLot>> lotsByCity;
  final List<OwnedBuildingLocation> myBuildingLocations;
  final Object? citiesError;
  final Object? purchaseError;

  /// Id returned from [purchaseLot] on success — the screen threads it into
  /// the BANK-specific follow-up mutations.
  final String purchasedBuildingId;

  final List<String> calls = [];
  Map<String, dynamic>? lastPurchaseArgs;

  @override
  Future<List<BuyBuildingCity>> fetchCities() async {
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
  Future<List<OwnedBuildingLocation>> fetchMyBuildingLocations(String companyId) async {
    calls.add('fetchMyBuildingLocations');
    return myBuildingLocations;
  }

  @override
  Future<String> purchaseLot({
    required String companyId,
    required String lotId,
    required String buildingType,
    String? buildingName,
    String? mediaType,
    String? powerPlantType,
  }) async {
    calls.add('purchaseLot');
    if (purchaseError != null) throw purchaseError!;
    lastPurchaseArgs = {
      'companyId': companyId,
      'lotId': lotId,
      'buildingType': buildingType,
      'buildingName': buildingName,
      'mediaType': mediaType,
      'powerPlantType': powerPlantType,
    };
    return purchasedBuildingId;
  }
}
