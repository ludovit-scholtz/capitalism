import 'package:capitalism_app/features/buildings/building_sales_models.dart';
import 'package:capitalism_app/features/buildings/building_sales_service.dart';
import 'package:capitalism_app/features/exchange/forex_models.dart' show MarketEvent;

class FakeBuildingSalesService implements BuildingSalesService {
  FakeBuildingSalesService({
    this.analytics,
    this.marketEvents = const [],
    this.flushResult = const FlushStorageResult(discardedItemCount: 0, totalDiscardedValue: 0),
    this.setThresholdError,
    this.flushStorageError,
  });

  final PublicSalesAnalytics? analytics;
  final List<MarketEvent> marketEvents;
  final FlushStorageResult flushResult;
  final Object? setThresholdError;
  final Object? flushStorageError;

  final List<String> calls = [];
  String? lastThresholdUnitId;
  double? lastThreshold;
  String? lastFlushedUnitId;

  @override
  Future<PublicSalesAnalytics?> fetchPublicSalesAnalytics(String unitId) async {
    calls.add('fetchPublicSalesAnalytics');
    return analytics;
  }

  @override
  Future<void> setInventoryAlertThreshold({required String buildingUnitId, required double? threshold}) async {
    calls.add('setInventoryAlertThreshold');
    if (setThresholdError != null) throw setThresholdError!;
    lastThresholdUnitId = buildingUnitId;
    lastThreshold = threshold;
  }

  @override
  Future<FlushStorageResult> flushStorage(String buildingUnitId) async {
    calls.add('flushStorage');
    if (flushStorageError != null) throw flushStorageError!;
    lastFlushedUnitId = buildingUnitId;
    return flushResult;
  }

  @override
  Future<List<MarketEvent>> fetchActiveMarketEvents(String? cityId) async {
    calls.add('fetchActiveMarketEvents');
    return marketEvents;
  }
}
