import '../../core/graphql/graphql_service.dart';
import 'operations_models.dart';

const _sessionQuery = r'''
  query GameAdminSession { gameAdminSession { canAccessAdminDashboard isRootAdministrator } }
''';

const _dashboardQuery = r'''
  query GameAdminDashboard {
    gameAdminDashboard {
      moneySupply totalPersonalCash totalCompanyCash externalMoneyInflowLast100Ticks totalShippingCostsLast100Ticks
      players { id email displayName role personalCash totalCompanyCash companyCount cityNames lastLoginAtUtc }
    }
  }
''';

const _statisticsQuery = r'''
  query OperationsStatistics($input: OperationsStatisticsInput) {
    operationsStatistics(input: $input) {
      currentTick range totalInflow totalOutflow netFlow totalPlayerCount totalCompanyCount totalBuildingCount
      inflowItems { category label amount percentage entryCount }
      outflowItems { category label amount percentage entryCount }
    }
  }
''';

const _productAnalyticsQuery = r'''
  query AdminProductAnalytics {
    adminProductAnalytics {
      windowTicks currentTick
      rows { productTypeId productName industry basePrice totalProduced activeManufacturerCount totalSold totalRevenue avgSellingPrice marketSize activeSellerCount activeCityCount }
    }
  }
''';

const _newsFeedQuery = r'''
  query OpsNewsManagerFeed {
    gameNewsFeed(includeDrafts: true) {
      unreadCount
      items { id entryType status targetServerKey createdByEmail updatedByEmail createdAtUtc updatedAtUtc publishedAtUtc isRead localizations { locale title summary htmlContent } }
    }
  }
''';

/// GraphQL calls for the 6 Operations (admin) screens. Deliberately
/// read-only — see `operations_screens.dart`'s top-of-file comment for the
/// full list of admin mutations (impersonation, NPC pause/resume, global
/// admin grants, end-shard, news publish/edit) that are not ported.
class OperationsService {
  const OperationsService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<bool> fetchCanAccessAdminDashboard() async {
    final result = await _graphQlService.request(_sessionQuery);
    final session = result['gameAdminSession'] as Map<String, dynamic>?;
    return session?['canAccessAdminDashboard'] as bool? ?? false;
  }

  Future<GameAdminDashboard> fetchDashboard() async {
    final result = await _graphQlService.request(_dashboardQuery);
    return GameAdminDashboard.fromJson(result['gameAdminDashboard'] as Map<String, dynamic>);
  }

  Future<OperationsStatistics> fetchStatistics(String range) async {
    final result = await _graphQlService.request(_statisticsQuery, variables: {'input': {'range': range}});
    return OperationsStatistics.fromJson(result['operationsStatistics'] as Map<String, dynamic>);
  }

  Future<List<ProductAnalyticsRow>> fetchProductAnalytics() async {
    final result = await _graphQlService.request(_productAnalyticsQuery);
    final data = result['adminProductAnalytics'] as Map<String, dynamic>;
    final rows = data['rows'] as List<dynamic>? ?? const [];
    return rows.map((e) => ProductAnalyticsRow.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<AdminNewsEntry>> fetchNewsFeed() async {
    final result = await _graphQlService.request(_newsFeedQuery);
    final feed = result['gameNewsFeed'] as Map<String, dynamic>;
    final items = feed['items'] as List<dynamic>? ?? const [];
    return items.map((e) => AdminNewsEntry.fromJson(e as Map<String, dynamic>)).toList();
  }
}
