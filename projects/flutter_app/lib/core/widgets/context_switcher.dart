// Persistent header control for switching person/company/city context,
// mirroring `projects/frontend/src/components/layout/ContextSwitcher.vue`
// (+ `ContextSwitcherPanel.vue`). Always visible in [AppShell]'s app bar
// once the player is signed in — a single, discoverable place for all three
// kinds of "who/where am I acting as" switching, rather than the scattered
// per-screen "pick a company" dropdowns used elsewhere in this app.

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:provider/provider.dart';

import '../auth/auth_state.dart';
import '../context/account_context_service.dart';
import '../context/account_context_state.dart';
import '../context/account_context_models.dart';
import '../graphql/graphql_service.dart';
import '../theme/app_icons.dart';
import '../theme/app_theme.dart';

class ContextSwitcher extends StatefulWidget {
  const ContextSwitcher({super.key, GraphQlService? graphQlService, AccountContextService? accountContextService})
    : _injectedGraphQlService = graphQlService,
      _injectedAccountContextService = accountContextService;

  final GraphQlService? _injectedGraphQlService;
  final AccountContextService? _injectedAccountContextService;

  @override
  State<ContextSwitcher> createState() => _ContextSwitcherState();
}

class _ContextSwitcherState extends State<ContextSwitcher> {
  late final AccountContextService _service;
  bool _bootstrapped = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedAccountContextService ?? AccountContextService(graphQlService);
    WidgetsBinding.instance.addPostFrameCallback((_) => _bootstrap());
  }

  Future<void> _bootstrap() async {
    if (_bootstrapped || !mounted) return;
    _bootstrapped = true;
    final state = context.read<AccountContextState>();
    await state.restoreSelectedCity();
    if (state.activeAccount == null && !state.loading) {
      await state.refresh(_service);
    }
  }

  void _openSwitcher() {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => _ContextSwitcherSheet(service: _service),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AccountContextState>();

    if (state.loading && state.activeAccount == null) {
      return const SizedBox(
        width: 20,
        height: 20,
        child: CircularProgressIndicator(strokeWidth: 2, color: AppTheme.neonCyan),
      );
    }

    final cityName = state.selectedCityName;
    final accountLabel = state.displayName;

    return InkWell(
      key: const Key('context-switcher-trigger'),
      onTap: _openSwitcher,
      borderRadius: BorderRadius.circular(8),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (cityName != null) ...[
              Flexible(child: Text(cityName, overflow: TextOverflow.ellipsis)),
              const Text(' · '),
            ],
            Flexible(
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  FaIcon(state.isCompanyMode ? AppIcons.business : AppIcons.brandMark, size: 12, color: AppTheme.neonCyan),
                  const SizedBox(width: 4),
                  Flexible(child: Text(accountLabel, overflow: TextOverflow.ellipsis)),
                ],
              ),
            ),
            const SizedBox(width: 2),
            const FaIcon(AppIcons.chevronDown, size: 10),
          ],
        ),
      ),
    );
  }
}

class _ContextSwitcherSheet extends StatefulWidget {
  const _ContextSwitcherSheet({required this.service});

  final AccountContextService service;

  @override
  State<_ContextSwitcherSheet> createState() => _ContextSwitcherSheetState();
}

class _ContextSwitcherSheetState extends State<_ContextSwitcherSheet> {
  String? _switchingCompanyId;
  bool _switchingToPerson = false;

  Future<void> _selectCity(String cityId) async {
    await context.read<AccountContextState>().selectCity(cityId);
    if (mounted) Navigator.of(context).pop();
  }

  Future<void> _selectPerson() async {
    setState(() => _switchingToPerson = true);
    final ok = await context.read<AccountContextState>().switchToPerson(widget.service);
    if (!mounted) return;
    setState(() => _switchingToPerson = false);
    if (ok) {
      Navigator.of(context).pop();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not switch to your person account.')));
    }
  }

  Future<void> _selectCompany(String companyId) async {
    setState(() => _switchingCompanyId = companyId);
    final ok = await context.read<AccountContextState>().switchToCompany(widget.service, companyId);
    if (!mounted) return;
    setState(() => _switchingCompanyId = null);
    if (ok) {
      Navigator.of(context).pop();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not switch to this company.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AccountContextState>();
    final theme = Theme.of(context);

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Switch context', style: theme.textTheme.titleLarge),
            const SizedBox(height: 16),
            if (state.error != null) Padding(padding: const EdgeInsets.only(bottom: 12), child: Text(state.error!)),
            Text('City', style: theme.textTheme.labelLarge?.copyWith(color: AppTheme.neonCyan)),
            if (state.cities.isEmpty)
              const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('No cities available yet.'))
            else
              ConstrainedBox(
                constraints: const BoxConstraints(maxHeight: 220),
                child: ListView(
                  shrinkWrap: true,
                  children: [
                    for (final city in state.cities)
                      ListTile(
                        key: ValueKey('context-city-${city.id}'),
                        dense: true,
                        leading: const FaIcon(AppIcons.city, size: 16),
                        title: Text(city.name),
                        subtitle: city.countryCode.isNotEmpty ? Text(city.countryCode) : null,
                        trailing: city.id == state.selectedCityId ? const FaIcon(AppIcons.check, size: 16, color: AppTheme.neonCyan) : null,
                        onTap: () => _selectCity(city.id),
                      ),
                  ],
                ),
              ),
            const Divider(height: 32),
            Text('Account', style: theme.textTheme.labelLarge?.copyWith(color: AppTheme.neonCyan)),
            ListTile(
              key: const Key('context-account-person'),
              dense: true,
              leading: const FaIcon(AppIcons.brandMark, size: 16),
              title: const Text('Person'),
              trailing: _switchingToPerson
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : (!state.isCompanyMode ? const FaIcon(AppIcons.check, size: 16, color: AppTheme.neonCyan) : null),
              onTap: state.isCompanyMode ? _selectPerson : null,
            ),
            for (final company in state.companies) _CompanyTile(company: company, state: state, switching: _switchingCompanyId == company.id, onTap: () => _selectCompany(company.id)),
          ],
        ),
      ),
    );
  }
}

class _CompanyTile extends StatelessWidget {
  const _CompanyTile({required this.company, required this.state, required this.switching, required this.onTap});

  final ContextCompanyOption company;
  final AccountContextState state;
  final bool switching;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isActive = state.isCompanyMode && state.activeCompanyId == company.id;
    return ListTile(
      key: ValueKey('context-company-${company.id}'),
      dense: true,
      leading: const FaIcon(AppIcons.business, size: 16),
      title: Text(company.name),
      subtitle: Text(company.cash.toStringAsFixed(0)),
      trailing: switching
          ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
          : (isActive ? const FaIcon(AppIcons.check, size: 16, color: AppTheme.neonCyan) : null),
      onTap: isActive ? null : onTap,
    );
  }
}
