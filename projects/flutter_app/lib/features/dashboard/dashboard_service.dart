import '../../core/graphql/graphql_service.dart';
import 'dashboard_models.dart';

const _onboardingGuardQuery = r'''
  query DashboardOnboardingGuard {
    me { onboardingCompletedAtUtc }
  }
''';

const _dashboardDataQuery = r'''
  query DashboardData {
    myCompanies {
      id name cash
      buildings { id name type level powerStatus destroyedAtUtc hasDefaultedCollateralLoan units { id } }
    }
    gameState { currentTick taxRate }
    myPendingActions { id actionType buildingName ticksRemaining }
  }
''';

/// GraphQL calls for the Dashboard screen, matching the exact operation
/// names/fields `DashboardView.vue`'s initial combined query uses (`myCompanies`
/// and `myPendingActions` are bare top-level auth-scoped fields, not nested
/// under `me` — verified against `Api/Types/Query.Rankings.Performance.cs`
/// and `Api/Types/Query.Operations.cs`).
class DashboardService {
  const DashboardService(this._graphQlService);

  final GraphQlService _graphQlService;

  /// Mirrors the web's guard: `null` means no player record (shouldn't
  /// happen once authenticated), `false` means onboarding still needs to
  /// happen, `true` means proceed to the dashboard.
  Future<bool?> fetchOnboardingCompleted() async {
    final data = await _graphQlService.request(_onboardingGuardQuery);
    final me = data['me'] as Map<String, dynamic>?;
    if (me == null) return null;
    return me['onboardingCompletedAtUtc'] != null;
  }

  Future<DashboardData> fetchDashboardData() async {
    final data = await _graphQlService.request(_dashboardDataQuery);
    final companies = ((data['myCompanies'] as List<dynamic>?) ?? const [])
        .map((c) => DashboardCompany.fromJson(c as Map<String, dynamic>))
        .toList();
    final gameState = data['gameState'] as Map<String, dynamic>?;
    final pendingActions = ((data['myPendingActions'] as List<dynamic>?) ?? const [])
        .map((a) => ScheduledAction.fromJson(a as Map<String, dynamic>))
        .toList();

    return DashboardData(
      companies: companies,
      currentTick: (gameState?['currentTick'] as num?)?.toInt() ?? 0,
      taxRate: (gameState?['taxRate'] as num?)?.toDouble() ?? 0,
      pendingActions: pendingActions,
    );
  }
}
