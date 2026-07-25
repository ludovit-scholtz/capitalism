// GraphQL calls for PUBLIC_SALES-specific tools (ROADMAP 135): sales
// analytics, the low-inventory alert threshold, "flush storage", and the
// market-event banner scoped to the building's city (ROADMAP 138a — grouped
// here rather than in a separate file since web fetches it as part of the
// same `loadPublicSalesAnalytics` flow, immediately after the analytics
// query resolves). Field names verified against `useBuildingDetail.ts`.

import '../../core/graphql/graphql_service.dart';
import '../exchange/forex_models.dart' show MarketEvent;
import 'building_sales_models.dart';

const _publicSalesAnalyticsQuery = r'''
  query PublicSalesAnalytics($unitId: UUID!) {
    publicSalesAnalytics(unitId: $unitId) {
      buildingUnitId productName totalRevenue totalProfit totalQuantitySold averagePricePerUnit currentSalesCapacity
      dataFromTick dataToTick demandSignal actionHint recentUtilization elasticityIndex trendDirection
      cityCurrencyCode cityMarketClearingPrice
      revenueHistory { tick revenue quantitySold }
      priceHistory { tick pricePerUnit }
      profitHistory { tick profit }
      marketShare { label share isUnmet }
    }
  }
''';

const _setInventoryAlertThresholdMutation = r'''
  mutation SetPublicSalesInventoryAlertThreshold($input: SetPublicSalesInventoryAlertThresholdInput!) {
    setPublicSalesInventoryAlertThreshold(input: $input) {
      buildingUnitId
      lowInventoryAlertThreshold
    }
  }
''';

const _flushStorageMutation = r'''
  mutation FlushStorage($input: FlushStorageInput!) {
    flushStorage(input: $input) {
      discardedItemCount
      totalDiscardedValue
    }
  }
''';

const _marketEventsQuery = r'''
  query BuildingMarketEvents($cityId: UUID) {
    getActiveMarketEvents(cityId: $cityId) { id title description magnitudeMultiplier ticksRemaining affectedResourceName }
  }
''';

class FlushStorageResult {
  const FlushStorageResult({required this.discardedItemCount, required this.totalDiscardedValue});

  final int discardedItemCount;
  final double totalDiscardedValue;

  factory FlushStorageResult.fromJson(Map<String, dynamic> json) => FlushStorageResult(
    discardedItemCount: (json['discardedItemCount'] as num?)?.toInt() ?? 0,
    totalDiscardedValue: (json['totalDiscardedValue'] as num?)?.toDouble() ?? 0,
  );
}

class BuildingSalesService {
  const BuildingSalesService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<PublicSalesAnalytics?> fetchPublicSalesAnalytics(String unitId) async {
    final result = await _graphQlService.request(_publicSalesAnalyticsQuery, variables: {'unitId': unitId});
    final data = result['publicSalesAnalytics'] as Map<String, dynamic>?;
    return data == null ? null : PublicSalesAnalytics.fromJson(data);
  }

  /// `threshold == null` disables the alert, mirroring web's "empty input
  /// clears the threshold" behavior.
  Future<void> setInventoryAlertThreshold({required String buildingUnitId, required double? threshold}) {
    return _graphQlService.request(
      _setInventoryAlertThresholdMutation,
      variables: {
        'input': {'buildingUnitId': buildingUnitId, 'minInventoryThreshold': threshold},
      },
    );
  }

  Future<FlushStorageResult> flushStorage(String buildingUnitId) async {
    final result = await _graphQlService.request(
      _flushStorageMutation,
      variables: {
        'input': {'buildingUnitId': buildingUnitId},
      },
    );
    return FlushStorageResult.fromJson(result['flushStorage'] as Map<String, dynamic>);
  }

  Future<List<MarketEvent>> fetchActiveMarketEvents(String? cityId) async {
    final result = await _graphQlService.request(_marketEventsQuery, variables: {'cityId': cityId});
    final list = result['getActiveMarketEvents'] as List<dynamic>? ?? const [];
    return list.map((e) => MarketEvent.fromJson(e as Map<String, dynamic>)).toList();
  }
}
