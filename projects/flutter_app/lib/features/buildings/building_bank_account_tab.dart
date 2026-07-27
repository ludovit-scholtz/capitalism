// Port of `BuildingBankAccountTab.vue` / `BuildingBankAccountPanel.vue`,
// trimmed like every other panel in this codebase: balance + currency +
// suspension status + an editable low-balance alert threshold. No full
// statement browser or cross-account reassignment controls — those are
// separate, larger features (`bank_statement_screen.dart` covers full
// statement browsing at the company level; a "move this building to a
// different account" picker is its own scope).

import 'package:flutter/material.dart';

import '../../core/theme/app_spacing.dart';
import 'building_panel_models.dart';
import 'building_panel_service.dart';

class BuildingBankAccountTab extends StatefulWidget {
  const BuildingBankAccountTab({super.key, required this.buildingId, required this.panelService});

  final String buildingId;
  final BuildingPanelService panelService;

  @override
  State<BuildingBankAccountTab> createState() => _BuildingBankAccountTabState();
}

class _BuildingBankAccountTabState extends State<BuildingBankAccountTab> {
  BuildingBankAccountInfo? _info;
  bool _loading = true;
  bool _thresholdSaving = false;
  String? _thresholdError;
  bool _thresholdSuccess = false;
  final TextEditingController _thresholdController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _thresholdController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final info = await widget.panelService.fetchBuildingBankAccount(widget.buildingId);
      if (!mounted) return;
      setState(() {
        _info = info;
        _thresholdController.text = info?.alertMinBalanceThreshold?.toString() ?? '';
        _loading = false;
      });
    } catch (_) {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _saveThreshold() async {
    final info = _info;
    if (info?.bankAccountId == null) return;
    final text = _thresholdController.text.trim();
    final parsed = text.isEmpty ? null : double.tryParse(text);
    if (text.isNotEmpty && parsed == null) {
      setState(() => _thresholdError = 'Enter a valid number.');
      return;
    }
    setState(() {
      _thresholdSaving = true;
      _thresholdError = null;
      _thresholdSuccess = false;
    });
    try {
      await widget.panelService.setBankAccountAlertThreshold(bankAccountId: info!.bankAccountId!, minBalanceThreshold: parsed);
      if (mounted) setState(() => _thresholdSuccess = true);
    } catch (_) {
      if (mounted) setState(() => _thresholdError = 'Could not save the alert threshold. Please try again.');
    } finally {
      if (mounted) setState(() => _thresholdSaving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (_loading) return const Center(child: Padding(padding: EdgeInsets.all(AppSpacing.md), child: CircularProgressIndicator()));

    final info = _info;
    if (info == null || !info.hasBankAccount) {
      return const Padding(padding: EdgeInsets.all(AppSpacing.md), child: Text('No bank account information is available for this building.'));
    }

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Bank Account', style: theme.textTheme.titleMedium),
              const SizedBox(height: AppSpacing.sm),
              Text('Balance', style: theme.textTheme.labelSmall),
              Text('${info.balance.toStringAsFixed(2)} ${info.currencyCode}', style: theme.textTheme.headlineSmall),
              if (info.accountNumber != null) ...[
                const SizedBox(height: 4),
                Text('Account ${info.accountNumber}', style: theme.textTheme.bodySmall),
              ],
              if (info.isSuspendedForFunds) ...[
                const SizedBox(height: AppSpacing.sm),
                Container(
                  padding: const EdgeInsets.all(AppSpacing.sm),
                  decoration: BoxDecoration(color: theme.colorScheme.errorContainer, borderRadius: BorderRadius.circular(8)),
                  child: Text(
                    info.suspendedReason ?? 'This building is suspended for insufficient funds.',
                    style: TextStyle(color: theme.colorScheme.onErrorContainer),
                  ),
                ),
              ],
              const SizedBox(height: AppSpacing.md),
              Text('Low-balance alert threshold', style: theme.textTheme.labelLarge),
              const SizedBox(height: 6),
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      key: const ValueKey('bank-account-threshold-field'),
                      controller: _thresholdController,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: InputDecoration(labelText: 'Threshold (${info.currencyCode})'),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  FilledButton(
                    key: const ValueKey('bank-account-threshold-save'),
                    onPressed: _thresholdSaving ? null : _saveThreshold,
                    child: _thresholdSaving ? const SizedBox(height: 16, width: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Text('Save'),
                  ),
                ],
              ),
              if (_thresholdSuccess) const Padding(padding: EdgeInsets.only(top: 4), child: Text('Saved.', style: TextStyle(color: Colors.green))),
              if (_thresholdError != null) Padding(padding: const EdgeInsets.only(top: 4), child: Text(_thresholdError!, style: const TextStyle(color: Colors.red))),
            ],
          ),
        ),
      ),
    );
  }
}
