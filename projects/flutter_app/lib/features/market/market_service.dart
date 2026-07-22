import '../../core/graphql/graphql_service.dart';
import 'market_models.dart';

const _citiesQuery = r'''
  query MarketCities { cities { id name currencyCode } }
''';

const _marketIntelligenceQuery = r'''
  query MarketIntelligence($cityId: UUID!) {
    marketIntelligence(cityId: $cityId) {
      cityId cityName dataFromTick dataToTick
      products {
        productTypeId productName productSlug totalWeeklySalesVolume
        sellers { rank companyId displayName askingPricePerUnit brandQuality estimatedWeeklySalesVolume marketShare }
      }
    }
  }
''';

const _marketOverviewQuery = r'''
  query MarketOverview($topN: Int!, $lastNTicks: Int!) {
    marketOverview(topN: $topN, lastNTicks: $lastNTicks) {
      cityId cityName currencyCode fromTick toTick
      products { productTypeId productName industry totalDemand totalQuantitySold satisfactionRate averageClearingPrice totalRevenue sellerCount topCompetitorCompanyName topCompetitorMarketSharePercent }
    }
  }
''';

const _competitorIntelligenceQuery = r'''
  query CompetitorIntelligence($cityId: UUID!, $productTypeId: UUID!) {
    competitorQualityIntelligence(cityId: $cityId, productTypeId: $productTypeId) {
      companyId companyName qualityLevel pricePremiumPct isOwnCompany
    }
  }
''';

const _energyMarketQuery = r'''
  query EnergyMarket($cityId: UUID!) {
    energyMarket(cityId: $cityId) {
      listingId buildingId buildingName companyId companyName cityId
      plantType pricePerKwhLocal capacityKw availableKw createdAtTick
    }
  }
''';

const _myPowerPlantsQuery = r'''
  query MyPowerPlants {
    myCompanies { id buildings { id name type cityId } }
  }
''';

const _listEnergyMutation = r'''
  mutation ListEnergyForSale($buildingId: UUID!, $pricePerKwhLocal: Decimal!, $capacityKw: Decimal!) {
    listEnergyForSale(input: { buildingId: $buildingId, pricePerKwhLocal: $pricePerKwhLocal, capacityKw: $capacityKw }) {
      listingId buildingId companyId cityId plantType pricePerKwhLocal capacityKw availableKw createdAtTick
    }
  }
''';

const _cancelEnergyListingMutation = r'''
  mutation CancelEnergyListing($listingId: UUID!) {
    cancelEnergyListing(input: { listingId: $listingId })
  }
''';

const _activeGlobalEventsQuery = r'''
  query ActiveGlobalEvents {
    activeGlobalEvents {
      id eventType severity title description isActive startTick durationTicks affectedCityId
      affectedCity { id name }
      operatingCostMultiplier tradeRouteMultiplier rdMultiplier mineEfficiencyMultiplier
      createdAtUtc resolvedAtUtc
    }
  }
''';

const _globalEventHistoryQuery = r'''
  query GlobalEventHistory($limit: Int!) {
    globalEventHistory(limit: $limit) {
      id eventType severity title description isActive startTick durationTicks affectedCityId
      affectedCity { id name }
      operatingCostMultiplier tradeRouteMultiplier rdMultiplier mineEfficiencyMultiplier
      createdAtUtc resolvedAtUtc
    }
  }
''';

const _myCompaniesQuery = r'''
  query MarketMyCompanies { myCompanies { id name cash } }
''';

const _campaignAnalyticsQuery = r'''
  query CampaignAnalytics($companyId: UUID!) {
    campaignAnalytics(companyId: $companyId) {
      companyId windowTicks totalRevenue totalMarketingSpend bestPerformingCity bestPerformingProduct globalRecommendation
      rows {
        buildingUnitId buildingId buildingName productName productTypeId cityName
        brandAwareness brandQuality marketingQuality currentPrice basePrice priceIndex pricePremiumPct
        revenueLastTicks quantityLastTicks utilizationRate trendDirection trendFactor demandSignal
        topPositiveFactor topNegativeFactor marketingSpendLastTicks brandRevenueBoost campaignImpact
        brandVsPriceBalance recommendation cityCurrencyCode
      }
    }
  }
''';

/// GraphQL calls for the Market Intelligence, Market Dashboard, Energy
/// Market, Global Events, and Marketing Analytics screens.
class MarketService {
  const MarketService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<Map<String, String>>> fetchCities() async {
    final result = await _graphQlService.request(_citiesQuery);
    final list = result['cities'] as List<dynamic>? ?? const [];
    return list
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<MarketIntelligence> fetchMarketIntelligence(String cityId) async {
    final result = await _graphQlService.request(_marketIntelligenceQuery, variables: {'cityId': cityId});
    return MarketIntelligence.fromJson(result['marketIntelligence'] as Map<String, dynamic>);
  }

  Future<MarketOverview> fetchMarketOverview(String cityId, {int topN = 10, int lastNTicks = 100}) async {
    final result = await _graphQlService.request(_marketOverviewQuery, variables: {'topN': topN, 'lastNTicks': lastNTicks});
    final overviews = result['marketOverview'] as List<dynamic>? ?? const [];
    final match = overviews.cast<Map<String, dynamic>>().where((o) => o['cityId'] == cityId);
    return MarketOverview.fromJson(match.isNotEmpty ? match.first : (overviews.isNotEmpty ? overviews.first as Map<String, dynamic> : {'cityId': cityId, 'products': []}));
  }

  Future<List<CompetitorQuality>> fetchCompetitorIntelligence({required String cityId, required String productTypeId}) async {
    final result = await _graphQlService.request(_competitorIntelligenceQuery, variables: {'cityId': cityId, 'productTypeId': productTypeId});
    final list = result['competitorQualityIntelligence'] as List<dynamic>? ?? const [];
    return list.map((e) => CompetitorQuality.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<EnergyListing>> fetchEnergyMarket(String cityId) async {
    final result = await _graphQlService.request(_energyMarketQuery, variables: {'cityId': cityId});
    final list = result['energyMarket'] as List<dynamic>? ?? const [];
    return list.map((e) => EnergyListing.fromJson(e as Map<String, dynamic>)).toList();
  }

  /// Returns power-plant buildings across the player's companies, keyed by
  /// `(id, name, cityId)` — flattened for the "List Surplus" picker.
  Future<List<Map<String, String>>> fetchMyPowerPlants() async {
    final result = await _graphQlService.request(_myPowerPlantsQuery);
    final companies = result['myCompanies'] as List<dynamic>? ?? const [];
    final plants = <Map<String, String>>[];
    for (final company in companies) {
      final buildings = (company as Map<String, dynamic>)['buildings'] as List<dynamic>? ?? const [];
      for (final building in buildings) {
        final buildingMap = building as Map<String, dynamic>;
        if (buildingMap['type'] == 'POWER_PLANT') {
          plants.add({'id': buildingMap['id'] as String, 'name': buildingMap['name'] as String, 'cityId': buildingMap['cityId'] as String});
        }
      }
    }
    return plants;
  }

  Future<void> listEnergyForSale({required String buildingId, required double pricePerKwhLocal, required double capacityKw}) {
    return _graphQlService.request(
      _listEnergyMutation,
      variables: {'buildingId': buildingId, 'pricePerKwhLocal': pricePerKwhLocal, 'capacityKw': capacityKw},
    );
  }

  Future<void> cancelEnergyListing(String listingId) {
    return _graphQlService.request(_cancelEnergyListingMutation, variables: {'listingId': listingId});
  }

  Future<List<GlobalEvent>> fetchActiveGlobalEvents() async {
    final result = await _graphQlService.request(_activeGlobalEventsQuery);
    final list = result['activeGlobalEvents'] as List<dynamic>? ?? const [];
    return list.map((e) => GlobalEvent.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<GlobalEvent>> fetchGlobalEventHistory({int limit = 20}) async {
    final result = await _graphQlService.request(_globalEventHistoryQuery, variables: {'limit': limit});
    final list = result['globalEventHistory'] as List<dynamic>? ?? const [];
    return list.map((e) => GlobalEvent.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Map<String, String>>> fetchMyCompanies() async {
    final result = await _graphQlService.request(_myCompaniesQuery);
    final companies = result['myCompanies'] as List<dynamic>? ?? const [];
    return companies
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'name': e['name'] as String})
        .toList();
  }

  Future<CampaignAnalytics> fetchCampaignAnalytics(String companyId) async {
    final result = await _graphQlService.request(_campaignAnalyticsQuery, variables: {'companyId': companyId});
    return CampaignAnalytics.fromJson(result['campaignAnalytics'] as Map<String, dynamic>);
  }
}
