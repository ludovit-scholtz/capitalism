// GraphQL calls for the City Economy tab's dashboards, matching the exact
// field/argument names `CityMapView.vue` uses (`fetchEconomyData`,
// `fetchCityWeatherForecast`, `fetchCityPowerBalance`,
// `fetchCityEconomicReport`) — several of these queries drop the `Get`
// prefix their C# resolver method names have (`GetCityWeatherForecast` →
// `cityWeatherForecast`) per the backend's HotChocolate naming convention,
// so field names here are copied from the web's query strings rather than
// derived from the C# method names.

import '../../core/graphql/graphql_service.dart';
import '../buildings/building_panel_models.dart';
import 'city_economy_models.dart';

const _cityEconomyQuery = r'''
  query CityEconomy($cityId: UUID!) {
    getCurrentEconomicCycle {
      id phase phaseStartedTick expectedDurationTicks intensityFactor phaseEndTick ticksRemaining
    }
    getActiveMarketEvents(cityId: $cityId) {
      id eventType title description magnitudeMultiplier startsAtTick expiresAtTick ticksRemaining
      affectedResourceTypeId affectedResourceName affectedResourceSlug
      affectedCityId affectedCityName
    }
    getEconomicHistory(last: 365) {
      tick phase intensityFactor
    }
  }
''';

const _cityWeatherForecastQuery = r'''
  query CityWeatherForecast($cityId: UUID!) {
    cityWeatherForecast(cityId: $cityId) {
      cityId currentWindPercent currentSolarPercent
      forecast { tick windPercent solarPercent }
    }
  }
''';

const _cityPowerBalanceQuery = r'''
  query CityEconomyPowerBalance($cityId: UUID!) {
    cityPowerBalance(cityId: $cityId) {
      totalSupplyMw totalDemandMw reserveMw reservePercent status powerPlantCount
    }
  }
''';

const _cityEconomicReportQuery = r'''
  query GetCityEconomicReport($cityId: UUID!) {
    getCityEconomicReport(cityId: $cityId) {
      latest {
        id taxCycleEnd totalSalaries totalPublicRevenue
        activeCompanies totalPowerConsumption totalPowerSupply averageProductQuality economicIndex
      }
      history {
        id taxCycleEnd totalSalaries totalPublicRevenue
        activeCompanies totalPowerConsumption totalPowerSupply averageProductQuality economicIndex
      }
    }
  }
''';

class CityEconomyData {
  const CityEconomyData({required this.economicCycle, required this.activeMarketEvents, required this.economicHistory});

  final EconomicCycleView? economicCycle;
  final List<MarketEventView> activeMarketEvents;
  final List<EconomicCycleHistoryPoint> economicHistory;
}

class CityEconomyService {
  const CityEconomyService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<CityEconomyData> fetchEconomyData(String cityId) async {
    final result = await _graphQlService.request(_cityEconomyQuery, variables: {'cityId': cityId});
    final cycleJson = result['getCurrentEconomicCycle'] as Map<String, dynamic>?;
    final events = (result['getActiveMarketEvents'] as List<dynamic>?) ?? const [];
    final history = (result['getEconomicHistory'] as List<dynamic>?) ?? const [];
    return CityEconomyData(
      economicCycle: cycleJson == null ? null : EconomicCycleView.fromJson(cycleJson),
      activeMarketEvents: events.map((e) => MarketEventView.fromJson(e as Map<String, dynamic>)).toList(),
      economicHistory: history.map((e) => EconomicCycleHistoryPoint.fromJson(e as Map<String, dynamic>)).toList(),
    );
  }

  Future<CityWeatherForecast?> fetchWeatherForecast(String cityId) async {
    final result = await _graphQlService.request(_cityWeatherForecastQuery, variables: {'cityId': cityId});
    final json = result['cityWeatherForecast'] as Map<String, dynamic>?;
    return json == null ? null : CityWeatherForecast.fromJson(json);
  }

  Future<CityPowerBalance?> fetchPowerBalance(String cityId) async {
    final result = await _graphQlService.request(_cityPowerBalanceQuery, variables: {'cityId': cityId});
    final json = result['cityPowerBalance'] as Map<String, dynamic>?;
    return json == null ? null : CityPowerBalance.fromJson(json);
  }

  Future<CityEconomicReportResult?> fetchEconomicReport(String cityId) async {
    final result = await _graphQlService.request(_cityEconomicReportQuery, variables: {'cityId': cityId});
    final json = result['getCityEconomicReport'] as Map<String, dynamic>?;
    return json == null ? null : CityEconomicReportResult.fromJson(json);
  }
}
