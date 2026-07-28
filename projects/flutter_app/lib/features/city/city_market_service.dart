// GraphQL call for the City Market tab's demand panel, matching the exact
// field/argument names `CityDemandPanel.vue` uses (`cityDemandSummary`).

import '../../core/graphql/graphql_service.dart';
import 'city_market_models.dart';

const _cityDemandSummaryQuery = r'''
  query CityDemandSummary($cityId: UUID!, $topN: Int!, $lastNTicks: Int!) {
    cityDemandSummary(cityId: $cityId, topN: $topN, lastNTicks: $lastNTicks) {
      cityId
      cityName
      currencyCode
      products {
        productTypeId
        productName
        industry
        totalDemand
        totalQuantitySold
        satisfactionRate
        averageClearingPrice
        sellerCount
      }
    }
  }
''';

class CityMarketService {
  const CityMarketService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<CityDemandSummary?> fetchDemandSummary(String cityId, {int topN = 5, int lastNTicks = 100}) async {
    final result = await _graphQlService.request(
      _cityDemandSummaryQuery,
      variables: {'cityId': cityId, 'topN': topN, 'lastNTicks': lastNTicks},
    );
    final json = result['cityDemandSummary'] as Map<String, dynamic>?;
    return json == null ? null : CityDemandSummary.fromJson(json);
  }
}
