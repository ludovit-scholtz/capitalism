// Ported from `projects/frontend/src/views/DashboardView.vue`. This is the
// web's "company mode" dashboard only (still no person-account mode /
// company switching — this app shows every company at once instead, a
// deliberate prior deviation from web kept as-is rather than undone here).
//
// Now has the 5-tab layout (Overview / Buildings / Activity / Chat / Pro,
// `DashboardMainContent.vue`) with live tick-based silent auto-refresh
// (subscribing to the app-wide `GameStateState`, the same
// `GameStatusBar._onGameStateChanged` pattern) in addition to
// pull-to-refresh. Overview shows one financial-summary card per company
// (web only ever has one active company) plus starter guidance, plus the
// Launch-New-Company flow. Buildings tab also shows a per-building
// revenue/cost/profit strip, a supply-chain status strip, and a per-city
// power-grid balance chip — all best-effort/error-isolated per
// building/city, matching web's `Promise.allSettled` pattern. Activity is
// the existing pending-actions list, relocated into its own tab. Chat is
// the existing placeholder content (`chat_panel.dart`) embedded inline —
// still no real chat feature. Pro shows an active/inactive badge derived
// from `me.proSubscriptionEndsAtUtc`, benefit cards, and a link out to the
// separate master web portal (`AppConfig.masterWebUrl`) — this app doesn't
// sell Pro itself, same as web.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/game_state/game_state_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/services/url_opener.dart';
import '../../core/theme/app_icons.dart';
import '../../core/widgets/icon_badge.dart';
import '../buildings/building_analytics_models.dart';
import '../buildings/building_panel_models.dart';
import '../company/company_models.dart';
import 'dashboard_models.dart';
import 'dashboard_new_company_card.dart';
import 'dashboard_overview_tab.dart';
import 'dashboard_pro_panel.dart';
import 'dashboard_service.dart';
import 'dashboard_widgets.dart';

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({
    super.key,
    GraphQlService? graphQlService,
    DashboardService? dashboardService,
    this.urlOpener = const ExternalUrlOpener(),
  }) : _injectedGraphQlService = graphQlService,
       _injectedDashboardService = dashboardService;

  final GraphQlService? _injectedGraphQlService;
  final DashboardService? _injectedDashboardService;

  /// Injectable so widget tests never hit a real browser/handler — see
  /// `test/support/fake_url_opener.dart`.
  final UrlOpener urlOpener;

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> with SingleTickerProviderStateMixin {
  late final DashboardService _service;
  late final TabController _tabController;
  late final GameStateState _gameStateState;
  int? _lastSeenTick;

  bool _loading = true;
  String? _error;
  DashboardData? _data;
  final Set<String> _removingBuildingIds = {};

  Map<String, CompanyLedger> _ledgers = {};
  bool _ledgersLoading = false;

  AdditionalCompanyPrerequisites? _newCompanyPrerequisites;
  List<NewCompanyCity> _newCompanyCities = const [];

  final Map<String, BuildingFinancialTimeline> _buildingFinancials = {};
  final Map<String, List<BuildingUnitOperationalStatus>> _unitStatuses = {};
  final Map<String, CityPowerBalance> _cityPowerBalances = {};

  String? _proSubscriptionEndsAtUtc;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedDashboardService ?? DashboardService(graphQlService);
    _tabController = TabController(length: 5, vsync: this);
    _gameStateState = context.read<GameStateState>();
    _gameStateState.addListener(_onGameStateChanged);
    // Defer past the current build — calling `context.go()` synchronously
    // from initState (as the unauthenticated-redirect branch below would,
    // since it returns before any `await`) fights with the Router, which is
    // still mid-build on the very first frame: "setState() or
    // markNeedsBuild() called during build."
    WidgetsBinding.instance.addPostFrameCallback((_) => _bootstrap());
  }

  @override
  void dispose() {
    _gameStateState.removeListener(_onGameStateChanged);
    _tabController.dispose();
    super.dispose();
  }

  /// Silently re-fetches on every server tick change, mirroring
  /// `GameStatusBar._onGameStateChanged` — `GameStatusBar` (always mounted
  /// in `AppShell`'s app bar once authenticated) owns the actual polling
  /// timer; this only reacts to the resulting `currentTick` changes.
  void _onGameStateChanged() {
    final tick = _gameStateState.gameState?.currentTick;
    if (tick == null || tick == _lastSeenTick) return;
    _lastSeenTick = tick;
    if (_data == null) return;
    unawaited(_silentRefresh());
  }

  Future<void> _bootstrap() async {
    final auth = context.read<AuthState>();
    if (!auth.isAuthenticated) {
      if (mounted) context.go('/login');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final onboardingCompleted = await _service.fetchOnboardingCompleted();
      if (onboardingCompleted == false) {
        if (mounted) context.go('/onboarding');
        return;
      }

      final data = await _service.fetchDashboardData();
      if (!mounted) return;
      setState(() {
        _data = data;
        _loading = false;
      });
      unawaited(_loadLedgers(data.companies));
      unawaited(_loadNewCompanyPrerequisites());
      unawaited(_loadBuildingAnalytics(data.companies));
      unawaited(_loadProSubscriptionStatus());
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load your dashboard. Please try again.';
        _loading = false;
      });
    }
  }

  /// Best-effort — a failure here just leaves the Pro tab showing "Inactive".
  Future<void> _loadProSubscriptionStatus() async {
    try {
      final endsAt = await _service.fetchProSubscriptionEndsAtUtc();
      if (!mounted) return;
      setState(() => _proSubscriptionEndsAtUtc = endsAt);
    } catch (_) {
      // Best-effort — see doc comment above.
    }
  }

  /// Best-effort — a failure here just leaves the Launch-New-Company card in
  /// its loading state rather than blocking the rest of the dashboard.
  Future<void> _loadNewCompanyPrerequisites() async {
    try {
      final (prerequisites, cities) = await _service.fetchAdditionalCompanyPrerequisites();
      if (!mounted) return;
      setState(() {
        _newCompanyPrerequisites = prerequisites;
        _newCompanyCities = cities;
      });
    } catch (_) {
      // Best-effort — see doc comment above.
    }
  }

  /// Fetches per-building financials/unit-statuses and per-city power
  /// balances, all in parallel and error-isolated per building/city
  /// (mirrors web's `Promise.allSettled` — one failure doesn't blank
  /// everything else, and results merge into the existing maps in place so
  /// a transient failure on refresh doesn't remove the last-known value).
  Future<void> _loadBuildingAnalytics(List<DashboardCompany> companies) async {
    final buildings = companies.expand((c) => c.buildings).toList();
    final cityIds = buildings.map((b) => b.cityId).where((id) => id.isNotEmpty).toSet();

    final financialsResults = await Future.wait(
      buildings.map((b) async {
        try {
          return MapEntry(b.id, await _service.fetchBuildingFinancials(b.id));
        } catch (_) {
          return null;
        }
      }),
    );
    final statusResults = await Future.wait(
      buildings.map((b) async {
        try {
          return MapEntry(b.id, await _service.fetchBuildingUnitStatuses(b.id));
        } catch (_) {
          return null;
        }
      }),
    );
    final powerResults = await Future.wait(
      cityIds.map((cityId) async {
        try {
          return MapEntry(cityId, await _service.fetchCityPowerBalance(cityId));
        } catch (_) {
          return null;
        }
      }),
    );

    if (!mounted) return;
    setState(() {
      for (final entry in financialsResults) {
        if (entry != null && entry.value != null) _buildingFinancials[entry.key] = entry.value!;
      }
      for (final entry in statusResults) {
        if (entry != null) _unitStatuses[entry.key] = entry.value;
      }
      for (final entry in powerResults) {
        if (entry != null && entry.value != null) _cityPowerBalances[entry.key] = entry.value!;
      }
    });
  }

  /// Launches the new company, then routes to `/buy-building/:companyId` —
  /// see the top-of-file comment on the deliberate deviation from web's
  /// (non-functional) `/onboarding?companyId=` redirect.
  Future<void> _launchNewCompany({required String companyName, required String cityId, required double ipoRaiseTarget}) async {
    final result = await _service.startAdditionalCompany(companyName: companyName, cityId: cityId, ipoRaiseTarget: ipoRaiseTarget);
    if (!mounted) return;
    context.go('/buy-building/${result.id}');
  }

  /// Best-effort, per-company: one company's ledger failing to load doesn't
  /// blank the others' financial cards.
  Future<void> _loadLedgers(List<DashboardCompany> companies, {bool isRefresh = false}) async {
    if (!isRefresh) setState(() => _ledgersLoading = true);
    final results = <String, CompanyLedger>{};
    for (final company in companies) {
      try {
        results[company.id] = await _service.fetchCompanyOverviewLedger(company.id);
      } catch (_) {
        // Best-effort — see doc comment above.
      }
    }
    if (!mounted) return;
    setState(() {
      _ledgers = results;
      _ledgersLoading = false;
    });
  }

  Future<void> _silentRefresh() async {
    try {
      final data = await _service.fetchDashboardData();
      if (!mounted) return;
      setState(() => _data = data);
      unawaited(_loadLedgers(data.companies, isRefresh: true));
      unawaited(_loadBuildingAnalytics(data.companies));
    } catch (_) {
      // Silent refresh failures are ignored — the last good data stays
      // visible rather than flashing an error over working content.
    }
  }

  /// Removes a destroyed building from the dashboard (ROADMAP 139), mirroring
  /// `removeDestroyedBuilding` on web but triggered straight from the tile.
  /// Optimistically drops the building from the in-memory list on success —
  /// there's nothing else on this screen that depends on it — and shows a
  /// `SnackBar` on failure rather than disturbing the rest of the list.
  Future<void> _removeBuilding(String companyId, String buildingId) async {
    setState(() => _removingBuildingIds.add(buildingId));
    try {
      await _service.removeDestroyedBuilding(buildingId);
      if (!mounted) return;
      final data = _data!;
      setState(() {
        _data = DashboardData(
          companies: [
            for (final company in data.companies)
              company.id == companyId
                  ? DashboardCompany(
                      id: company.id,
                      name: company.name,
                      cash: company.cash,
                      buildings: company.buildings.where((b) => b.id != buildingId).toList(),
                    )
                  : company,
          ],
          currentTick: data.currentTick,
          taxRate: data.taxRate,
          pendingActions: data.pendingActions,
        );
      });
    } catch (e) {
      if (mounted) {
        final message = e is GraphQlException ? e.message : 'Could not remove this building. Please try again.';
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      }
    } finally {
      if (mounted) setState(() => _removingBuildingIds.remove(buildingId));
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: _bootstrap, child: const Text('Try Again')),
            ],
          ),
        ),
      );
    }

    final data = _data!;
    if (data.companies.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const IconBadge(icon: AppIcons.business, size: 64, iconSize: 28),
              const SizedBox(height: 12),
              const Text('You do not have a company yet.'),
              const SizedBox(height: 12),
              FilledButton(onPressed: () => context.go('/onboarding'), child: const Text('Start Onboarding')),
            ],
          ),
        ),
      );
    }

    final theme = Theme.of(context);

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 24, 24, 0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Dashboard', style: theme.textTheme.headlineSmall),
              const SizedBox(height: 4),
              Text('Tick ${data.currentTick} · Tax ${data.taxRate.toStringAsFixed(1)}%', style: theme.textTheme.bodyMedium),
            ],
          ),
        ),
        TabBar(
          controller: _tabController,
          isScrollable: true,
          tabs: const [
            Tab(text: 'Overview'),
            Tab(text: 'Buildings'),
            Tab(text: 'Activity'),
            Tab(text: 'Chat'),
            Tab(text: 'Pro'),
          ],
        ),
        Expanded(
          child: TabBarView(
            controller: _tabController,
            children: [
              RefreshIndicator(
                onRefresh: _silentRefresh,
                child: DashboardOverviewTab(
                  companies: data.companies,
                  ledgers: _ledgers,
                  ledgersLoading: _ledgersLoading,
                  newCompanyCard: DashboardNewCompanyCard(
                    prerequisites: _newCompanyPrerequisites,
                    cities: _newCompanyCities,
                    onLaunch: _launchNewCompany,
                  ),
                ),
              ),
              RefreshIndicator(
                onRefresh: _silentRefresh,
                child: ListView(
                  // See the matching comment in `dashboard_overview_tab.dart`.
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(24),
                  children: [
                    for (final company in data.companies)
                      DashboardCompanyCard(
                        company: company,
                        onRemoveBuilding: (buildingId) => _removeBuilding(company.id, buildingId),
                        removingBuildingIds: _removingBuildingIds,
                        buildingFinancials: _buildingFinancials,
                        unitStatuses: _unitStatuses,
                        cityPowerBalances: _cityPowerBalances,
                      ),
                  ],
                ),
              ),
              RefreshIndicator(
                onRefresh: _silentRefresh,
                child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(24),
                  children: [DashboardPendingActionsSection(actions: data.pendingActions)],
                ),
              ),
              Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const IconBadge(icon: AppIcons.chat, size: 48, iconSize: 20),
                      const SizedBox(height: 12),
                      Text(
                        'Not implemented yet. Mirrors the chat side panel in AppHeader.vue.',
                        textAlign: TextAlign.center,
                        style: theme.textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
              ),
              DashboardProPanel(proSubscriptionEndsAtUtc: _proSubscriptionEndsAtUtc, urlOpener: widget.urlOpener),
            ],
          ),
        ),
      ],
    );
  }
}
