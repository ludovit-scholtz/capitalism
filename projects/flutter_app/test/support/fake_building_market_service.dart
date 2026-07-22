import 'package:capitalism_app/features/buildings/building_market_models.dart';
import 'package:capitalism_app/features/buildings/building_market_service.dart';

class FakeBuildingMarketService implements BuildingMarketService {
  FakeBuildingMarketService({
    this.market = const [],
    this.myListings = const [],
    this.cities = const [],
    this.myCompanies = const [],
    this.marketError,
    this.listingsError,
    this.actionError,
  });

  final List<MarketBuilding> market;
  final List<MyBuildingListing> myListings;
  final List<Map<String, String>> cities;
  final List<Map<String, String>> myCompanies;
  final Object? marketError;
  final Object? listingsError;
  final Object? actionError;

  final List<String> calls = [];
  Map<String, dynamic>? lastOfferArgs;
  final List<String> acceptedOfferIds = [];
  final List<String> rejectedOfferIds = [];

  @override
  Future<List<MarketBuilding>> fetchMarket({String? cityId, String? buildingType, double? maxPrice}) async {
    calls.add('fetchMarket');
    if (marketError != null) throw marketError!;
    return market;
  }

  @override
  Future<List<MyBuildingListing>> fetchMyListings() async {
    calls.add('fetchMyListings');
    if (listingsError != null) throw listingsError!;
    return myListings;
  }

  @override
  Future<List<Map<String, String>>> fetchCities() async {
    calls.add('fetchCities');
    return cities;
  }

  @override
  Future<List<Map<String, String>>> fetchMyCompanies() async {
    calls.add('fetchMyCompanies');
    return myCompanies;
  }

  @override
  Future<void> makeOffer({required String buildingId, required String buyerCompanyId, required double offeredPrice}) async {
    calls.add('makeOffer');
    if (actionError != null) throw actionError!;
    lastOfferArgs = {'buildingId': buildingId, 'buyerCompanyId': buyerCompanyId, 'offeredPrice': offeredPrice};
  }

  @override
  Future<void> acceptOffer({required String offerId, required int offerVersion}) async {
    calls.add('acceptOffer');
    if (actionError != null) throw actionError!;
    acceptedOfferIds.add(offerId);
  }

  @override
  Future<void> rejectOffer({required String offerId, required int offerVersion}) async {
    calls.add('rejectOffer');
    if (actionError != null) throw actionError!;
    rejectedOfferIds.add(offerId);
  }
}
