// GraphQL calls for the building analytics panels (ROADMAP 137): unit
// resource history, the financial timeline, the recent-activity feed, the
// supply-chain diagram, and per-unit product analytics. Field names/limits
// verified against `useBuildingDetail.ts`'s corresponding `load*` functions.

import '../../core/graphql/graphql_service.dart';
import 'building_analytics_models.dart';

const _unitResourceHistoriesQuery = r'''
  query BuildingUnitResourceHistories($buildingId: UUID!, $limit: Int) {
    buildingUnitResourceHistories(buildingId: $buildingId, limit: $limit) {
      buildingUnitId tick inflowQuantity outflowQuantity consumedQuantity producedQuantity
    }
  }
''';

const _financialTimelineQuery = r'''
  query BuildingFinancialTimeline($buildingId: UUID!, $limit: Int) {
    buildingFinancialTimeline(buildingId: $buildingId, limit: $limit) {
      dataFromTick dataToTick totalSales totalCosts totalProfit
      timeline { tick sales costs profit }
    }
  }
''';

const _recentActivityQuery = r'''
  query BuildingRecentActivity($buildingId: UUID!, $limit: Int) {
    buildingRecentActivity(buildingId: $buildingId, limit: $limit) {
      tick eventType description quantity amount
    }
  }
''';

const _supplyChainQuery = r'''
  query BuildingSupplyChain($buildingId: UUID!) {
    buildingSupplyChain(buildingId: $buildingId) {
      units { buildingUnitId unitType gridX gridY status idleTicks fillPercent resourceOrProductName }
      links { fromUnitId toUnitId estimatedTransitCost }
      healthScore healthReason
    }
  }
''';

const _unitProductAnalyticsQuery = r'''
  query UnitProductAnalytics($unitId: UUID!) {
    unitProductAnalytics(unitId: $unitId) {
      buildingUnitId productName dataFromTick dataToTick
      totalCost totalQuantityProduced estimatedRevenue estimatedProfit cityCurrencyCode
      snapshots { tick totalCost quantityProduced estimatedRevenue estimatedProfit }
    }
  }
''';

class BuildingAnalyticsService {
  const BuildingAnalyticsService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<UnitResourceHistoryPoint>> fetchUnitResourceHistories(String buildingId, {int limit = 60}) async {
    final result = await _graphQlService.request(_unitResourceHistoriesQuery, variables: {'buildingId': buildingId, 'limit': limit});
    final list = result['buildingUnitResourceHistories'] as List<dynamic>? ?? const [];
    return list.map((e) => UnitResourceHistoryPoint.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<BuildingFinancialTimeline?> fetchFinancialTimeline(String buildingId, {int limit = 100}) async {
    final result = await _graphQlService.request(_financialTimelineQuery, variables: {'buildingId': buildingId, 'limit': limit});
    final data = result['buildingFinancialTimeline'] as Map<String, dynamic>?;
    return data == null ? null : BuildingFinancialTimeline.fromJson(data);
  }

  Future<List<BuildingRecentActivityEvent>> fetchRecentActivity(String buildingId, {int limit = 30}) async {
    final result = await _graphQlService.request(_recentActivityQuery, variables: {'buildingId': buildingId, 'limit': limit});
    final list = result['buildingRecentActivity'] as List<dynamic>? ?? const [];
    return list.map((e) => BuildingRecentActivityEvent.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<BuildingSupplyChainDiagram?> fetchSupplyChain(String buildingId) async {
    final result = await _graphQlService.request(_supplyChainQuery, variables: {'buildingId': buildingId});
    final data = result['buildingSupplyChain'] as Map<String, dynamic>?;
    return data == null ? null : BuildingSupplyChainDiagram.fromJson(data);
  }

  Future<UnitProductAnalytics?> fetchUnitProductAnalytics(String unitId) async {
    final result = await _graphQlService.request(_unitProductAnalyticsQuery, variables: {'unitId': unitId});
    final data = result['unitProductAnalytics'] as Map<String, dynamic>?;
    return data == null ? null : UnitProductAnalytics.fromJson(data);
  }
}
