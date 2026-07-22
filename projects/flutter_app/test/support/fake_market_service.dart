import 'package:capitalism_app/features/market/market_models.dart';
import 'package:capitalism_app/features/market/market_service.dart';

class FakeMarketService implements MarketService {
  FakeMarketService({
    this.cities = const [],
    this.intelligenceByCity = const {},
    this.overviewByCity = const {},
    this.competitors = const [],
    this.energyListingsByCity = const {},
    this.myPowerPlants = const [],
    this.activeEvents = const [],
    this.eventHistory = const [],
    this.myCompanies = const [],
    this.campaignAnalytics,
    this.loadError,
    this.actionError,
  });

  final List<Map<String, String>> cities;
  final Map<String, MarketIntelligence> intelligenceByCity;
  final Map<String, MarketOverview> overviewByCity;
  final List<CompetitorQuality> competitors;
  final Map<String, List<EnergyListing>> energyListingsByCity;
  final List<Map<String, String>> myPowerPlants;
  final List<GlobalEvent> activeEvents;
  final List<GlobalEvent> eventHistory;
  final List<Map<String, String>> myCompanies;
  final CampaignAnalytics? campaignAnalytics;
  final Object? loadError;
  final Object? actionError;

  final List<String> calls = [];
  Map<String, dynamic>? lastListEnergyArgs;
  String? cancelledListingId;

  @override
  Future<List<Map<String, String>>> fetchCities() async {
    calls.add('fetchCities');
    if (loadError != null) throw loadError!;
    return cities;
  }

  @override
  Future<MarketIntelligence> fetchMarketIntelligence(String cityId) async {
    calls.add('fetchMarketIntelligence');
    return intelligenceByCity[cityId] ?? const MarketIntelligence(cityName: '', products: []);
  }

  @override
  Future<MarketOverview> fetchMarketOverview(String cityId, {int topN = 10, int lastNTicks = 100}) async {
    calls.add('fetchMarketOverview');
    return overviewByCity[cityId] ?? MarketOverview(cityId: cityId, cityName: '', products: const []);
  }

  @override
  Future<List<CompetitorQuality>> fetchCompetitorIntelligence({required String cityId, required String productTypeId}) async {
    calls.add('fetchCompetitorIntelligence');
    return competitors;
  }

  @override
  Future<List<EnergyListing>> fetchEnergyMarket(String cityId) async {
    calls.add('fetchEnergyMarket');
    return energyListingsByCity[cityId] ?? const [];
  }

  @override
  Future<List<Map<String, String>>> fetchMyPowerPlants() async {
    calls.add('fetchMyPowerPlants');
    return myPowerPlants;
  }

  @override
  Future<void> listEnergyForSale({required String buildingId, required double pricePerKwhLocal, required double capacityKw}) async {
    calls.add('listEnergyForSale');
    if (actionError != null) throw actionError!;
    lastListEnergyArgs = {'buildingId': buildingId, 'pricePerKwhLocal': pricePerKwhLocal, 'capacityKw': capacityKw};
  }

  @override
  Future<void> cancelEnergyListing(String listingId) async {
    calls.add('cancelEnergyListing');
    if (actionError != null) throw actionError!;
    cancelledListingId = listingId;
  }

  @override
  Future<List<GlobalEvent>> fetchActiveGlobalEvents() async {
    calls.add('fetchActiveGlobalEvents');
    if (loadError != null) throw loadError!;
    return activeEvents;
  }

  @override
  Future<List<GlobalEvent>> fetchGlobalEventHistory({int limit = 20}) async {
    calls.add('fetchGlobalEventHistory');
    return eventHistory;
  }

  @override
  Future<List<Map<String, String>>> fetchMyCompanies() async {
    calls.add('fetchMyCompanies');
    return myCompanies;
  }

  @override
  Future<CampaignAnalytics> fetchCampaignAnalytics(String companyId) async {
    calls.add('fetchCampaignAnalytics');
    if (loadError != null) throw loadError!;
    return campaignAnalytics!;
  }
}
