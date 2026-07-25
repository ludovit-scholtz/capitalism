// Ported from `projects/frontend/src/views/DashboardView.vue`. This is the
// web's "company mode" dashboard only, deliberately trimmed of a lot of
// secondary surface area (documented in ROADMAP.md and
// .github/copilot-instructions.md — check there before assuming parity on
// anything not covered here): no person-account mode / multi-company
// switching / "Launch New Company" modal, no 5-tab layout (Overview /
// Buildings / Activity / Chat / Pro) — buildings and pending actions are
// just shown together on one scrollable screen — no Pro subscription panel,
// no per-city power-grid balance summary, no per-building
// revenue/cost/profit ledger panel or supply-chain unit-status panel, no
// currency-code-aware formatting (cash is shown with a plain `$` prefix,
// not the ledger-derived `primaryCurrencyCode`), and no live tick-based
// auto-polling refresh of this screen's own data — a pull-to-refresh
// instead re-fetches silently, matching the spirit of the web's "no
// loading-spinner flash on background refresh" behavior without needing
// tick polling. A shared tick clock (`core/game_state/game_state_state.dart`)
// does now exist app-wide, driving the nav bar's balance/tick indicator
// (`core/widgets/game_status_bar.dart`) — this screen just doesn't
// subscribe to it itself yet.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/theme/app_icons.dart';
import '../../core/widgets/icon_badge.dart';
import 'dashboard_models.dart';
import 'dashboard_service.dart';
import 'dashboard_widgets.dart';

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key, GraphQlService? graphQlService, DashboardService? dashboardService})
    : _injectedGraphQlService = graphQlService,
      _injectedDashboardService = dashboardService;

  final GraphQlService? _injectedGraphQlService;
  final DashboardService? _injectedDashboardService;

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  late final DashboardService _service;

  bool _loading = true;
  String? _error;
  DashboardData? _data;
  final Set<String> _removingBuildingIds = {};

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedDashboardService ?? DashboardService(graphQlService);
    // Defer past the current build — calling `context.go()` synchronously
    // from initState (as the unauthenticated-redirect branch below would,
    // since it returns before any `await`) fights with the Router, which is
    // still mid-build on the very first frame: "setState() or
    // markNeedsBuild() called during build."
    WidgetsBinding.instance.addPostFrameCallback((_) => _bootstrap());
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
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load your dashboard. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _silentRefresh() async {
    try {
      final data = await _service.fetchDashboardData();
      if (!mounted) return;
      setState(() => _data = data);
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

    return RefreshIndicator(
      onRefresh: _silentRefresh,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Dashboard', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text(
            'Tick ${data.currentTick} · Tax ${data.taxRate.toStringAsFixed(1)}%',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 16),
          for (final company in data.companies)
            DashboardCompanyCard(
              company: company,
              onRemoveBuilding: (buildingId) => _removeBuilding(company.id, buildingId),
              removingBuildingIds: _removingBuildingIds,
            ),
          DashboardPendingActionsSection(actions: data.pendingActions),
        ],
      ),
    );
  }
}
