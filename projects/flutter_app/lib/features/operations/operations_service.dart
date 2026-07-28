import '../../core/graphql/graphql_service.dart';
import 'operations_models.dart';

const _sessionQuery = r'''
  query GameAdminSession {
    gameAdminSession {
      canAccessAdminDashboard isRootAdministrator hasGlobalAdminRole isImpersonating
      adminActor { displayName }
      effectivePlayer { displayName }
    }
  }
''';

const _dashboardQuery = r'''
  query GameAdminDashboard {
    gameAdminDashboard {
      moneySupply totalPersonalCash totalCompanyCash externalMoneyInflowLast100Ticks totalShippingCostsLast100Ticks
      players { id email displayName role isInvisibleInChat personalCash totalCompanyCash companyCount cityNames lastLoginAtUtc }
    }
  }
''';

const _npcCompaniesQuery = r'''
  query OpsNpcCompanies {
    npcCompanies { id name archetype difficultyLevel homeCityName isActive buildingCount }
  }
''';

const _pauseNpcMutation = r'''
  mutation PauseNpcCompany($input: ManageNpcCompanyActivityInput!) {
    pauseNpcCompany(input: $input) { id isActive }
  }
''';

const _resumeNpcMutation = r'''
  mutation ResumeNpcCompany($input: ManageNpcCompanyActivityInput!) {
    resumeNpcCompany(input: $input) { id isActive }
  }
''';

const _setPlayerInvisibleInChatMutation = r'''
  mutation SetPlayerInvisibleInChat($input: SetPlayerInvisibleInChatInput!) {
    setPlayerInvisibleInChat(input: $input) { id isInvisibleInChat }
  }
''';

const _setLocalGameAdminRoleMutation = r'''
  mutation SetLocalGameAdminRole($input: SetLocalGameAdminRoleInput!) {
    setLocalGameAdminRole(input: $input) { id role }
  }
''';

const _assignGlobalGameAdminRoleMutation = r'''
  mutation AssignGlobalGameAdminRole($input: ManageGlobalGameAdminRoleInput!) {
    assignGlobalGameAdminRole(input: $input) { email }
  }
''';

const _removeGlobalGameAdminRoleMutation = r'''
  mutation RemoveGlobalGameAdminRole($input: ManageGlobalGameAdminRoleInput!) {
    removeGlobalGameAdminRole(input: $input)
  }
''';

const _endShardManuallyMutation = r'''
  mutation EndShardManually($input: EndShardManuallyInput!) {
    endShardManually(input: $input) { gameEnded winnerDisplayName }
  }
''';

const _upsertGameNewsEntryMutation = r'''
  mutation UpsertGameNewsEntry($input: UpsertGameNewsEntryInput!) {
    upsertGameNewsEntry(input: $input) { id entryType status }
  }
''';

const _startImpersonationMutation = r'''
  mutation StartAdminImpersonation($input: StartAdminImpersonationInput!) {
    startAdminImpersonation(input: $input) { token expiresAtUtc }
  }
''';

const _stopImpersonationMutation = r'''
  mutation StopAdminImpersonation {
    stopAdminImpersonation { token expiresAtUtc }
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

/// GraphQL calls for the 6 Operations (admin) screens, including the admin
/// mutations (impersonation, NPC pause/resume, global admin grants,
/// end-shard, news publish/edit, chat-visibility) — see
/// `operations_screens.dart`'s top-of-file comment for what is still
/// intentionally trimmed.
class OperationsService {
  const OperationsService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<bool> fetchCanAccessAdminDashboard() async {
    final session = await fetchSession();
    return session.canAccessAdminDashboard;
  }

  Future<GameAdminSessionInfo> fetchSession() async {
    final result = await _graphQlService.request(_sessionQuery);
    return GameAdminSessionInfo.fromJson((result['gameAdminSession'] as Map<String, dynamic>?) ?? const {});
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

  Future<List<NpcCompanySummary>> fetchNpcCompanies() async {
    final result = await _graphQlService.request(_npcCompaniesQuery);
    final list = result['npcCompanies'] as List<dynamic>? ?? const [];
    return list.map((e) => NpcCompanySummary.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> pauseNpcCompany(String npcCompanyId) {
    return _graphQlService.request(
      _pauseNpcMutation,
      variables: {
        'input': {'npcCompanyId': npcCompanyId},
      },
    );
  }

  Future<void> resumeNpcCompany(String npcCompanyId) {
    return _graphQlService.request(
      _resumeNpcMutation,
      variables: {
        'input': {'npcCompanyId': npcCompanyId},
      },
    );
  }

  Future<void> setPlayerInvisibleInChat({required String playerId, required bool isInvisible}) {
    return _graphQlService.request(
      _setPlayerInvisibleInChatMutation,
      variables: {
        'input': {'playerId': playerId, 'isInvisibleInChat': isInvisible},
      },
    );
  }

  /// Root-administrator only (per-server `PlayerRole.Admin` toggle).
  Future<void> setLocalGameAdminRole({required String playerId, required bool isAdmin}) {
    return _graphQlService.request(
      _setLocalGameAdminRoleMutation,
      variables: {
        'input': {'playerId': playerId, 'isAdmin': isAdmin},
      },
    );
  }

  /// Root-administrator only (cross-server Master API admin grant).
  Future<void> assignGlobalGameAdminRole(String email) {
    return _graphQlService.request(
      _assignGlobalGameAdminRoleMutation,
      variables: {
        'input': {'email': email},
      },
    );
  }

  /// Root-administrator only (cross-server Master API admin grant).
  Future<void> removeGlobalGameAdminRole(String email) {
    return _graphQlService.request(
      _removeGlobalGameAdminRoleMutation,
      variables: {
        'input': {'email': email},
      },
    );
  }

  Future<void> endShardManually({String? reason}) {
    return _graphQlService.request(
      _endShardManuallyMutation,
      variables: {
        'input': {'reason': reason},
      },
    );
  }

  Future<void> upsertGameNewsEntry({
    String? entryId,
    required String entryType,
    required String status,
    required List<Map<String, String>> localizations,
  }) {
    return _graphQlService.request(
      _upsertGameNewsEntryMutation,
      variables: {
        'input': {'entryId': entryId, 'entryType': entryType, 'status': status, 'localizations': localizations},
      },
    );
  }

  /// Returns the new bearer token for the impersonated player — the caller
  /// must apply it via `AuthState.setToken` to actually switch sessions.
  Future<String> startImpersonation({required String targetPlayerId, String accountType = 'PERSON', String? companyId}) async {
    final result = await _graphQlService.request(
      _startImpersonationMutation,
      variables: {
        'input': {'targetPlayerId': targetPlayerId, 'accountType': accountType, 'companyId': companyId},
      },
    );
    return (result['startAdminImpersonation'] as Map<String, dynamic>)['token'] as String;
  }

  /// Returns the admin actor's original bearer token — the caller must
  /// apply it via `AuthState.setToken` to switch back.
  Future<String> stopImpersonation() async {
    final result = await _graphQlService.request(_stopImpersonationMutation);
    return (result['stopAdminImpersonation'] as Map<String, dynamic>)['token'] as String;
  }
}
