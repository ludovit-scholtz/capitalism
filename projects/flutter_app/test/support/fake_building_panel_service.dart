import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:capitalism_app/features/buildings/building_panel_service.dart';

class FakeBuildingPanelService implements BuildingPanelService {
  FakeBuildingPanelService({
    this.apartmentDetail,
    this.companyBrands = const [],
    this.powerPlantAnalytics,
    this.cityPowerBalance,
    this.cityMediaHouses = const [],
    this.mediaHouseUnits = const [],
    this.actionError,
  });

  final ApartmentBuildingDetail? apartmentDetail;
  final List<CompanyBrand> companyBrands;
  final PowerPlantAnalytics? powerPlantAnalytics;
  final CityPowerBalance? cityPowerBalance;
  final List<CityMediaHouse> cityMediaHouses;
  final List<MediaHouseUnitConfig> mediaHouseUnits;
  final Object? actionError;

  final List<String> calls = [];
  double? lastRentPerSqm;
  int? lastDispatchPercent;
  int? lastPriority;
  (double price, double capacity)? lastListing;
  String? lastCancelledListingId;
  double? lastContentBudget;
  Map<String, dynamic>? lastUnitConfig;

  @override
  Future<ApartmentBuildingDetail?> fetchApartmentBuildingDetail(String buildingId) async {
    calls.add('fetchApartmentBuildingDetail');
    return apartmentDetail;
  }

  @override
  Future<void> setRentPerSqm({required String buildingId, required double rentPerSqm}) async {
    calls.add('setRentPerSqm');
    if (actionError != null) throw actionError!;
    lastRentPerSqm = rentPerSqm;
  }

  @override
  Future<List<CompanyBrand>> fetchCompanyBrands(String companyId) async {
    calls.add('fetchCompanyBrands');
    return companyBrands;
  }

  @override
  Future<PowerPlantAnalytics?> fetchPowerPlantAnalytics(String buildingId) async {
    calls.add('fetchPowerPlantAnalytics');
    return powerPlantAnalytics;
  }

  @override
  Future<CityPowerBalance?> fetchCityPowerBalance(String cityId) async {
    calls.add('fetchCityPowerBalance');
    return cityPowerBalance;
  }

  @override
  Future<void> setPlantDispatch({required String buildingId, required int dispatchTargetPercent}) async {
    calls.add('setPlantDispatch');
    if (actionError != null) throw actionError!;
    lastDispatchPercent = dispatchTargetPercent;
  }

  @override
  Future<void> setPowerPriority({required String buildingId, required int priority}) async {
    calls.add('setPowerPriority');
    if (actionError != null) throw actionError!;
    lastPriority = priority;
  }

  @override
  Future<void> listEnergyForSale({required String buildingId, required double pricePerKwhLocal, required double capacityKw}) async {
    calls.add('listEnergyForSale');
    if (actionError != null) throw actionError!;
    lastListing = (pricePerKwhLocal, capacityKw);
  }

  @override
  Future<void> cancelEnergyListing(String listingId) async {
    calls.add('cancelEnergyListing');
    if (actionError != null) throw actionError!;
    lastCancelledListingId = listingId;
  }

  @override
  Future<List<CityMediaHouse>> fetchCityMediaHouses(String cityId) async {
    calls.add('fetchCityMediaHouses');
    return cityMediaHouses;
  }

  @override
  Future<List<MediaHouseUnitConfig>> fetchMediaHouseUnits(String buildingId) async {
    calls.add('fetchMediaHouseUnits');
    return mediaHouseUnits;
  }

  @override
  Future<void> setMediaHouseContentBudget({required String buildingId, required double contentBudgetPerTick}) async {
    calls.add('setMediaHouseContentBudget');
    if (actionError != null) throw actionError!;
    lastContentBudget = contentBudgetPerTick;
  }

  @override
  Future<void> upgradeMediaHouse(String buildingId) async {
    calls.add('upgradeMediaHouse');
    if (actionError != null) throw actionError!;
  }

  @override
  Future<void> configureMediaHouseUnit({
    required String buildingId,
    String? unitId,
    required String targetCompanyId,
    required String mediaType,
    required double campaignBudgetPerTick,
    required bool isActive,
  }) async {
    calls.add('configureMediaHouseUnit');
    if (actionError != null) throw actionError!;
    lastUnitConfig = {
      'unitId': unitId,
      'targetCompanyId': targetCompanyId,
      'mediaType': mediaType,
      'campaignBudgetPerTick': campaignBudgetPerTick,
      'isActive': isActive,
    };
  }
}
