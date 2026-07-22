// Mobile port of `projects/frontend/src/components/BankAccountTransferPanel.vue`
// (used generically elsewhere on the web, but only wired up here on the
// Forex Exchange screen's Transfer tab). Calls the real `transferFunds`
// mutation — both accounts must be owned by the caller and share a
// currency; cross-currency transfers go through the Swap tab instead
// (server-enforced, not re-validated client-side here).

import 'package:flutter/material.dart';

import 'forex_models.dart';
import 'forex_service.dart';

class BankTransferSection extends StatefulWidget {
  const BankTransferSection({super.key, required this.forexService});

  final ForexService forexService;

  @override
  State<BankTransferSection> createState() => _BankTransferSectionState();
}

class _BankTransferSectionState extends State<BankTransferSection> {
  bool _loading = true;
  String? _error;
  List<BankAccountOption> _accounts = const [];

  String? _fromAccountId;
  String? _toAccountId;
  final _amountController = TextEditingController();
  final _descriptionController = TextEditingController();
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _amountController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final accounts = await widget.forexService.fetchMyBankAccounts();
      if (!mounted) return;
      setState(() {
        _accounts = accounts;
        _fromAccountId ??= accounts.isNotEmpty ? accounts.first.id : null;
        _toAccountId ??= accounts.length > 1 ? accounts[1].id : null;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load your bank accounts. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _submitTransfer() async {
    final fromId = _fromAccountId;
    final toId = _toAccountId;
    final amount = double.tryParse(_amountController.text);
    if (fromId == null || toId == null || amount == null || amount <= 0) return;
    if (fromId == toId) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Choose two different accounts.')));
      return;
    }

    setState(() => _submitting = true);
    try {
      final result = await widget.forexService.transferFunds(
        fromBankAccountId: fromId,
        toBankAccountId: toId,
        amount: amount,
        description: _descriptionController.text.trim().isEmpty ? null : _descriptionController.text.trim(),
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Transferred ${result.amount.toStringAsFixed(2)} ${result.currencyCode}.')),
        );
      }
      _amountController.clear();
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Transfer failed. Please try again.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Column(children: [Text(_error!), const SizedBox(height: 8), OutlinedButton(onPressed: _load, child: const Text('Try again'))]);
    }
    if (_accounts.length < 2) {
      return const Text('You need at least two bank accounts to transfer funds.');
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        DropdownButtonFormField<String>(
          key: const Key('transfer-from-account'),
          initialValue: _fromAccountId,
          decoration: const InputDecoration(labelText: 'From account'),
          items: [for (final account in _accounts) DropdownMenuItem(value: account.id, child: Text('${account.accountNumber} (${account.currencyCode}) · ${account.balance.toStringAsFixed(2)}'))],
          onChanged: (value) => setState(() => _fromAccountId = value),
        ),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          key: const Key('transfer-to-account'),
          initialValue: _toAccountId,
          decoration: const InputDecoration(labelText: 'To account'),
          items: [for (final account in _accounts) DropdownMenuItem(value: account.id, child: Text('${account.accountNumber} (${account.currencyCode})'))],
          onChanged: (value) => setState(() => _toAccountId = value),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: _amountController,
          decoration: const InputDecoration(labelText: 'Amount'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
        ),
        const SizedBox(height: 8),
        TextField(controller: _descriptionController, decoration: const InputDecoration(labelText: 'Description (optional)')),
        const SizedBox(height: 12),
        FilledButton(onPressed: _submitting ? null : _submitTransfer, child: Text(_submitting ? 'Transferring…' : 'Transfer')),
      ],
    );
  }
}
