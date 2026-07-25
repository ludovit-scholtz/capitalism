import 'package:capitalism_app/features/buildings/building_analytics_models.dart';
import 'package:capitalism_app/features/buildings/building_analytics_service.dart';

class FakeBuildingAnalyticsService implements BuildingAnalyticsService {
  FakeBuildingAnalyticsService({
    this.resourceHistories = const [],
    this.financialTimeline,
    this.recentActivity = const [],
    this.supplyChain,
    this.productAnalytics,
  });

  final List<UnitResourceHistoryPoint> resourceHistories;
  final BuildingFinancialTimeline? financialTimeline;
  final List<BuildingRecentActivityEvent> recentActivity;
  final BuildingSupplyChainDiagram? supplyChain;
  final UnitProductAnalytics? productAnalytics;

  final List<String> calls = [];

  @override
  Future<List<UnitResourceHistoryPoint>> fetchUnitResourceHistories(String buildingId, {int limit = 60}) async {
    calls.add('fetchUnitResourceHistories');
    return resourceHistories;
  }

  @override
  Future<BuildingFinancialTimeline?> fetchFinancialTimeline(String buildingId, {int limit = 100}) async {
    calls.add('fetchFinancialTimeline');
    return financialTimeline;
  }

  @override
  Future<List<BuildingRecentActivityEvent>> fetchRecentActivity(String buildingId, {int limit = 30}) async {
    calls.add('fetchRecentActivity');
    return recentActivity;
  }

  @override
  Future<BuildingSupplyChainDiagram?> fetchSupplyChain(String buildingId) async {
    calls.add('fetchSupplyChain');
    return supplyChain;
  }

  @override
  Future<UnitProductAnalytics?> fetchUnitProductAnalytics(String unitId) async {
    calls.add('fetchUnitProductAnalytics');
    return productAnalytics;
  }
}
