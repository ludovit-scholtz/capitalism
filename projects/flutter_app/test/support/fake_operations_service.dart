import 'package:capitalism_app/features/operations/operations_models.dart';
import 'package:capitalism_app/features/operations/operations_service.dart';

class FakeOperationsService implements OperationsService {
  FakeOperationsService({
    this.canAccess = true,
    this.dashboard,
    this.statistics,
    this.productAnalytics = const [],
    this.newsFeed = const [],
    this.loadError,
  });

  final bool canAccess;
  final GameAdminDashboard? dashboard;
  final OperationsStatistics? statistics;
  final List<ProductAnalyticsRow> productAnalytics;
  final List<AdminNewsEntry> newsFeed;
  final Object? loadError;

  final List<String> calls = [];

  @override
  Future<bool> fetchCanAccessAdminDashboard() async {
    calls.add('fetchCanAccessAdminDashboard');
    return canAccess;
  }

  @override
  Future<GameAdminDashboard> fetchDashboard() async {
    calls.add('fetchDashboard');
    if (loadError != null) throw loadError!;
    return dashboard!;
  }

  @override
  Future<OperationsStatistics> fetchStatistics(String range) async {
    calls.add('fetchStatistics');
    if (loadError != null) throw loadError!;
    return statistics!;
  }

  @override
  Future<List<ProductAnalyticsRow>> fetchProductAnalytics() async {
    calls.add('fetchProductAnalytics');
    if (loadError != null) throw loadError!;
    return productAnalytics;
  }

  @override
  Future<List<AdminNewsEntry>> fetchNewsFeed() async {
    calls.add('fetchNewsFeed');
    if (loadError != null) throw loadError!;
    return newsFeed;
  }
}
