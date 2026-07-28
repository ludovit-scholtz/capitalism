// Ported from `projects/frontend/src/views/CompanySettingsView.vue` —
// factored out of `company_screens.dart` to keep that file under the
// 500-line budget. Includes the `proposeDividend` form (the mutation was
// already wired in `CompanyService` but not exposed in the UI) alongside
// the existing name/dividend-ratio/salary settings and dividend voting.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'company_models.dart';
import 'company_service.dart';

class CompanySettingsScreen extends StatefulWidget {
  const CompanySettingsScreen({super.key, required this.companyId, GraphQlService? graphQlService, CompanyService? companyService})
    : _injectedGraphQlService = graphQlService,
      _injectedCompanyService = companyService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final CompanyService? _injectedCompanyService;

  @override
  State<CompanySettingsScreen> createState() => _CompanySettingsScreenState();
}

class _CompanySettingsScreenState extends State<CompanySettingsScreen> {
  late final CompanyService _service;

  bool _loading = true;
  String? _error;
  CompanySettings? _settings;
  final _nameController = TextEditingController();
  final _dividendController = TextEditingController();
  final _dividendProposalController = TextEditingController();
  final Map<String, double> _salaryMultipliers = {};
  bool _saving = false;
  bool _dividendBusy = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedCompanyService ?? CompanyService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _dividendController.dispose();
    _dividendProposalController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final settings = await _service.fetchCompanySettings(widget.companyId);
      if (!mounted) return;
      setState(() {
        _settings = settings;
        _nameController.text = settings.companyName;
        _dividendController.text = settings.dividendPayoutRatio.toStringAsFixed(2);
        _dividendProposalController.text = (settings.dividendPayoutRatio * 100).toStringAsFixed(2);
        _salaryMultipliers.clear();
        for (final city in settings.citySalarySettings) {
          _salaryMultipliers[city.cityId] = city.salaryMultiplier;
        }
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load company settings. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      await _service.updateCompanySettings(
        companyId: widget.companyId,
        name: _nameController.text.trim(),
        dividendPayoutRatio: double.tryParse(_dividendController.text) ?? 0,
        citySalarySettings: [for (final entry in _salaryMultipliers.entries) {'cityId': entry.key, 'salaryMultiplier': entry.value}],
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Settings saved.')));
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not save settings.')));
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _proposeDividend() async {
    final percent = double.tryParse(_dividendProposalController.text);
    if (percent == null) return;
    setState(() => _dividendBusy = true);
    try {
      await _service.proposeDividend(companyId: widget.companyId, dividendPercent: percent);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Dividend proposal submitted.')));
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not submit the dividend proposal.')));
      }
    } finally {
      if (mounted) setState(() => _dividendBusy = false);
    }
  }

  Future<void> _voteDividend(bool approve) async {
    setState(() => _dividendBusy = true);
    try {
      await _service.voteDividend(companyId: widget.companyId, approve: approve);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Vote recorded.')));
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not record your vote.')));
      }
    } finally {
      if (mounted) setState(() => _dividendBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [Text(_error!), const SizedBox(height: 12), OutlinedButton(onPressed: _load, child: const Text('Try again'))],
          ),
        ),
      );
    }

    final settings = _settings!;
    final theme = Theme.of(context);
    final proposal = settings.pendingDividendProposal;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Company Settings', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 16),
        TextField(controller: _nameController, decoration: const InputDecoration(labelText: 'Company name')),
        const SizedBox(height: 8),
        TextField(
          controller: _dividendController,
          decoration: const InputDecoration(labelText: 'Dividend payout ratio (0-1)'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
        ),
        const SizedBox(height: 16),
        Text('Administration', style: theme.textTheme.titleMedium),
        Text('Overhead rate: ${(settings.administrationOverheadRate * 100).toStringAsFixed(1)}%'),
        Text('Age factor: ${settings.ageFactor.toStringAsFixed(2)} · Asset factor: ${settings.assetFactor.toStringAsFixed(2)}'),
        const SizedBox(height: 16),
        Text('Salaries by city', style: theme.textTheme.titleMedium),
        for (final city in settings.citySalarySettings)
          Row(
            children: [
              Expanded(child: Text(city.cityName)),
              SizedBox(
                width: 120,
                child: Slider(
                  value: (_salaryMultipliers[city.cityId] ?? city.salaryMultiplier).clamp(0.5, 2.0),
                  min: 0.5,
                  max: 2.0,
                  divisions: 15,
                  label: (_salaryMultipliers[city.cityId] ?? city.salaryMultiplier).toStringAsFixed(2),
                  onChanged: (value) => setState(() => _salaryMultipliers[city.cityId] = value),
                ),
              ),
            ],
          ),
        const SizedBox(height: 16),
        FilledButton(onPressed: _saving ? null : _save, child: Text(_saving ? 'Saving…' : 'Save changes')),
        const SizedBox(height: 24),
        Text('Dividend', style: theme.textTheme.titleMedium),
        Text('Current dividend rate: ${(settings.dividendPayoutRatio * 100).toStringAsFixed(1)}%'),
        const SizedBox(height: 8),
        TextField(
          key: const ValueKey('dividend-proposal-field'),
          controller: _dividendProposalController,
          decoration: const InputDecoration(labelText: 'Propose a new dividend payout (%)'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
        ),
        const SizedBox(height: 8),
        FilledButton(
          onPressed: _dividendBusy || proposal != null ? null : _proposeDividend,
          child: Text(_dividendBusy ? 'Submitting…' : 'Propose dividend'),
        ),
        const SizedBox(height: 16),
        if (proposal == null)
          const Text('No pending dividend proposal.')
        else
          Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Pending proposal: ${proposal.dividendPercent.toStringAsFixed(1)}%'),
                  Text('Voting closes at tick ${proposal.votingCloseTick} (${proposal.ticksRemaining} ticks left)'),
                  const SizedBox(height: 8),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(3),
                    child: LinearProgressIndicator(
                      value: proposal.approvePercent / 100,
                      minHeight: 8,
                      backgroundColor: theme.colorScheme.surfaceContainerHighest,
                      color: Colors.green.shade600,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text('Approve ${proposal.approvePercent}% · Reject ${proposal.rejectPercent}% (For: ${proposal.forVotes} · Against: ${proposal.againstVotes})'),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      FilledButton(onPressed: _dividendBusy ? null : () => _voteDividend(true), child: const Text('Approve')),
                      const SizedBox(width: 8),
                      OutlinedButton(onPressed: _dividendBusy ? null : () => _voteDividend(false), child: const Text('Reject')),
                    ],
                  ),
                ],
              ),
            ),
          ),
      ],
    );
  }
}
