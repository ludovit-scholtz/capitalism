import '../../core/graphql/graphql_service.dart';
import '../buildings/building_analytics_models.dart';
import '../buildings/building_panel_models.dart';
import '../company/company_models.dart';
import 'dashboard_models.dart';

const _onboardingGuardQuery = r'''
  query DashboardOnboardingGuard {
    me { onboardingCompletedAtUtc }
  }
''';

/// Pro tab's status badge — a separate `me`-scoped query (matching this
/// app's existing convention of each screen fetching its own minimal `me`
/// subset rather than one shared player-profile fetch).
const _proSubscriptionQuery = r'''
  query DashboardProSubscription {
    me { proSubscriptionEndsAtUtc }
  }
''';

const _dashboardDataQuery = r'''
  query DashboardData {
    myCompanies {
      id name cash
      buildings { id name type level powerStatus destroyedAtUtc hasDefaultedCollateralLoan cityId units { id unitType gridX gridY } }
    }
    gameState { currentTick taxRate }
    myPendingActions { id actionType buildingName ticksRemaining }
  }
''';

/// Overview tab's financial summary — a subset of the full `companyLedger`
/// fields the standalone Ledger screen fetches (`company_service.dart`),
/// reusing the same `CompanyLedger` model rather than inventing a new type.
const _companyOverviewLedgerQuery = r'''
  query DashboardCompanyOverviewLedger($companyId: UUID!) {
    companyLedger(companyId: $companyId) {
      companyName gameYear currentCash primaryCurrencyCode
      totalRevenue totalPurchasingCosts totalShippingCosts totalLaborCosts totalEnergyCosts
      totalMarketingCosts totalTaxPaid totalOtherCosts netIncome totalAssets
    }
  }
''';

/// "Launch New Company" eligibility + city choices, mirroring
/// `NewCompanyModal.vue`'s bootstrap query.
const _additionalCompanyPrerequisitesQuery = r'''
  query DashboardAdditionalCompanyPrerequisites {
    additionalCompanyPrerequisites {
      companyCount underMaxCap hasExistingCompany companyAgeRequirementMet
      ticksUntilAgeRequirementMet profitabilityRequirementMet balanceRequirementMet allRequirementsMet
    }
    cities { id name currencyCode }
  }
''';

const _startAdditionalCompanyMutation = r'''
  mutation DashboardStartAdditionalCompany($input: StartAdditionalCompanyInput!) {
    startAdditionalCompany(input: $input) { id name }
  }
''';

/// Per-building compact financial strip — reuses `BuildingFinancialTimeline`
/// (`building_analytics_models.dart`) but only requests the 3 totals (no
/// `timeline` array), matching web's dashboard-only usage of this query
/// (the full timeline backs a separate building-detail chart).
const _buildingFinancialsQuery = r'''
  query DashboardBuildingFinancials($buildingId: UUID!) {
    buildingFinancialTimeline(buildingId: $buildingId) { totalSales totalCosts totalProfit }
  }
''';

/// Supply-chain strip — a distinct, simpler query from the FACTORY-only 4x4
/// diagram's `buildingSupplyChain`.
const _buildingUnitStatusesQuery = r'''
  query DashboardBuildingUnitStatuses($buildingId: UUID!) {
    buildingUnitOperationalStatuses(buildingId: $buildingId) {
      buildingUnitId status blockedCode blockedReason idleTicks
    }
  }
''';

/// Public, no-auth query — reused as-is from `building_panel_service.dart`.
const _cityPowerBalanceQuery = r'''
  query DashboardCityPowerBalance($cityId: UUID!) {
    cityPowerBalance(cityId: $cityId) { totalSupplyMw totalDemandMw reserveMw reservePercent status }
  }
''';

/// Mirrors `useBuildingDetail.ts`'s `removeDestroyedBuilding()` — on mobile
/// this is triggered from the dashboard tile directly rather than requiring
/// a trip through the building detail screen (ROADMAP 139).
const _removeDestroyedBuildingMutation = r'''
  mutation RemoveDestroyedBuilding($input: RemoveDestroyedBuildingInput!) {
    removeDestroyedBuilding(input: $input) { id }
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

  Future<String?> fetchProSubscriptionEndsAtUtc() async {
    final data = await _graphQlService.request(_proSubscriptionQuery);
    final me = data['me'] as Map<String, dynamic>?;
    return me?['proSubscriptionEndsAtUtc'] as String?;
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

  Future<CompanyLedger> fetchCompanyOverviewLedger(String companyId) async {
    final data = await _graphQlService.request(_companyOverviewLedgerQuery, variables: {'companyId': companyId});
    return CompanyLedger.fromJson(data['companyLedger'] as Map<String, dynamic>);
  }

  Future<(AdditionalCompanyPrerequisites, List<NewCompanyCity>)> fetchAdditionalCompanyPrerequisites() async {
    final data = await _graphQlService.request(_additionalCompanyPrerequisitesQuery);
    final prerequisites = AdditionalCompanyPrerequisites.fromJson(
      data['additionalCompanyPrerequisites'] as Map<String, dynamic>,
    );
    final cities = ((data['cities'] as List<dynamic>?) ?? const [])
        .map((c) => NewCompanyCity.fromJson(c as Map<String, dynamic>))
        .toList();
    return (prerequisites, cities);
  }

  Future<NewCompanyResult> startAdditionalCompany({
    required String companyName,
    required String cityId,
    required double ipoRaiseTarget,
  }) async {
    final data = await _graphQlService.request(
      _startAdditionalCompanyMutation,
      variables: {
        'input': {'companyName': companyName, 'cityId': cityId, 'ipoRaiseTarget': ipoRaiseTarget},
      },
    );
    return NewCompanyResult.fromJson(data['startAdditionalCompany'] as Map<String, dynamic>);
  }

  Future<BuildingFinancialTimeline?> fetchBuildingFinancials(String buildingId) async {
    final data = await _graphQlService.request(_buildingFinancialsQuery, variables: {'buildingId': buildingId});
    final financials = data['buildingFinancialTimeline'] as Map<String, dynamic>?;
    return financials == null ? null : BuildingFinancialTimeline.fromJson(financials);
  }

  Future<List<BuildingUnitOperationalStatus>> fetchBuildingUnitStatuses(String buildingId) async {
    final data = await _graphQlService.request(_buildingUnitStatusesQuery, variables: {'buildingId': buildingId});
    final list = (data['buildingUnitOperationalStatuses'] as List<dynamic>?) ?? const [];
    return list.map((e) => BuildingUnitOperationalStatus.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<CityPowerBalance?> fetchCityPowerBalance(String cityId) async {
    final data = await _graphQlService.request(_cityPowerBalanceQuery, variables: {'cityId': cityId});
    final balance = data['cityPowerBalance'] as Map<String, dynamic>?;
    return balance == null ? null : CityPowerBalance.fromJson(balance);
  }

  Future<void> removeDestroyedBuilding(String buildingId) {
    return _graphQlService.request(
      _removeDestroyedBuildingMutation,
      variables: {
        'input': {'buildingId': buildingId},
      },
    );
  }
}
