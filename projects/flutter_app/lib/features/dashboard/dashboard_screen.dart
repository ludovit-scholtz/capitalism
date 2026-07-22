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
// auto-polling refresh (that needs a shared game-state/tick subscription
// this app doesn't have yet) — a pull-to-refresh instead re-fetches
// silently, matching the spirit of the web's "no loading-spinner flash on
// background refresh" behavior without needing tick polling.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
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
              const Icon(Icons.business_outlined, size: 48),
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
          for (final company in data.companies) DashboardCompanyCard(company: company),
          DashboardPendingActionsSection(actions: data.pendingActions),
        ],
      ),
    );
  }
}
