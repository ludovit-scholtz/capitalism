import 'package:capitalism_app/features/operations/operations_models.dart';
import 'package:capitalism_app/features/operations/operations_service.dart';

class FakeOperationsService implements OperationsService {
  FakeOperationsService({
    this.canAccess = true,
    this.isRootAdministrator = false,
    this.hasGlobalAdminRole = false,
    this.isImpersonating = false,
    this.adminActorDisplayName,
    this.effectivePlayerDisplayName,
    this.dashboard,
    this.statistics,
    this.productAnalytics = const [],
    this.newsFeed = const [],
    this.npcCompanies = const [],
    this.loadError,
    this.actionError,
    this.impersonationToken = 'new-token',
  });

  final bool canAccess;
  final bool isRootAdministrator;
  final bool hasGlobalAdminRole;
  final bool isImpersonating;
  final String? adminActorDisplayName;
  final String? effectivePlayerDisplayName;
  final GameAdminDashboard? dashboard;
  final OperationsStatistics? statistics;
  final List<ProductAnalyticsRow> productAnalytics;
  final List<AdminNewsEntry> newsFeed;
  final List<NpcCompanySummary> npcCompanies;
  final Object? loadError;
  final Object? actionError;
  final String impersonationToken;

  final List<String> calls = [];
  String? lastPausedNpcId;
  String? lastResumedNpcId;
  Map<String, dynamic>? lastInvisibleInChatArgs;
  Map<String, dynamic>? lastLocalAdminArgs;
  String? lastGrantedGlobalAdminEmail;
  String? lastRevokedGlobalAdminEmail;
  String? lastEndShardReason;
  Map<String, dynamic>? lastNewsEntry;

  @override
  Future<bool> fetchCanAccessAdminDashboard() async {
    calls.add('fetchCanAccessAdminDashboard');
    return canAccess;
  }

  @override
  Future<GameAdminSessionInfo> fetchSession() async {
    calls.add('fetchSession');
    return GameAdminSessionInfo(
      canAccessAdminDashboard: canAccess,
      isRootAdministrator: isRootAdministrator,
      hasGlobalAdminRole: hasGlobalAdminRole,
      isImpersonating: isImpersonating,
      adminActorDisplayName: adminActorDisplayName,
      effectivePlayerDisplayName: effectivePlayerDisplayName,
    );
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

  @override
  Future<List<NpcCompanySummary>> fetchNpcCompanies() async {
    calls.add('fetchNpcCompanies');
    if (loadError != null) throw loadError!;
    return npcCompanies;
  }

  @override
  Future<void> pauseNpcCompany(String npcCompanyId) async {
    calls.add('pauseNpcCompany');
    if (actionError != null) throw actionError!;
    lastPausedNpcId = npcCompanyId;
  }

  @override
  Future<void> resumeNpcCompany(String npcCompanyId) async {
    calls.add('resumeNpcCompany');
    if (actionError != null) throw actionError!;
    lastResumedNpcId = npcCompanyId;
  }

  @override
  Future<void> setPlayerInvisibleInChat({required String playerId, required bool isInvisible}) async {
    calls.add('setPlayerInvisibleInChat');
    if (actionError != null) throw actionError!;
    lastInvisibleInChatArgs = {'playerId': playerId, 'isInvisible': isInvisible};
  }

  @override
  Future<void> setLocalGameAdminRole({required String playerId, required bool isAdmin}) async {
    calls.add('setLocalGameAdminRole');
    if (actionError != null) throw actionError!;
    lastLocalAdminArgs = {'playerId': playerId, 'isAdmin': isAdmin};
  }

  @override
  Future<void> assignGlobalGameAdminRole(String email) async {
    calls.add('assignGlobalGameAdminRole');
    if (actionError != null) throw actionError!;
    lastGrantedGlobalAdminEmail = email;
  }

  @override
  Future<void> removeGlobalGameAdminRole(String email) async {
    calls.add('removeGlobalGameAdminRole');
    if (actionError != null) throw actionError!;
    lastRevokedGlobalAdminEmail = email;
  }

  @override
  Future<void> endShardManually({String? reason}) async {
    calls.add('endShardManually');
    if (actionError != null) throw actionError!;
    lastEndShardReason = reason;
  }

  @override
  Future<void> upsertGameNewsEntry({
    String? entryId,
    required String entryType,
    required String status,
    required List<Map<String, String>> localizations,
  }) async {
    calls.add('upsertGameNewsEntry');
    if (actionError != null) throw actionError!;
    lastNewsEntry = {'entryId': entryId, 'entryType': entryType, 'status': status, 'localizations': localizations};
  }

  @override
  Future<String> startImpersonation({required String targetPlayerId, String accountType = 'PERSON', String? companyId}) async {
    calls.add('startImpersonation');
    if (actionError != null) throw actionError!;
    return impersonationToken;
  }

  @override
  Future<String> stopImpersonation() async {
    calls.add('stopImpersonation');
    if (actionError != null) throw actionError!;
    return impersonationToken;
  }
}
