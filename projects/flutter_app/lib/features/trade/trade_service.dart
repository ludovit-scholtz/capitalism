import '../../core/graphql/graphql_service.dart';
import 'trade_models.dart';

const _myTradeRoutesQuery = r'''
  query MyTradeRoutes {
    myTradeRoutes {
      id companyId
      sourceBuildingId sourceBuildingName sourceCityName sourceCurrencyCode
      destinationBuildingId destinationBuildingName destinationCityName destinationCurrencyCode
      productTypeId productTypeName resourceTypeId resourceTypeName
      quantity quality pricePerUnit
      scheduledDepartureTick expectedArrivalTick transitTicks
      shippingCostEstimate shippingCostActual
      status failureReason
      createdAtUtc departedAtUtc completedAtUtc
    }
  }
''';

/// GraphQL calls for the Trade Routes screen, matching
/// `Api/Types/Query.TradeRoutes.cs`'s `myTradeRoutes` query — owner-scoped
/// server-side, no explicit company argument.
class TradeService {
  const TradeService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<TradeRoute>> fetchMyTradeRoutes() async {
    final result = await _graphQlService.request(_myTradeRoutesQuery);
    final list = result['myTradeRoutes'] as List<dynamic>? ?? const [];
    return list.map((e) => TradeRoute.fromJson(e as Map<String, dynamic>)).toList();
  }
}
