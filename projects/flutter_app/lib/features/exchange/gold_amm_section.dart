// Mobile port of `projects/frontend/src/components/forex/GoldAmmSection.vue`
// — a constant-product AMM for fiat↔gold swaps and liquidity provision.
// Trimmed for mobile: liquidity add/create/remove flows use plain dialogs
// with manual amount entry rather than the web's live-slippage-aware form
// (the underlying mutations are the real ones, just a simpler input UX).

import 'package:flutter/material.dart';

import 'forex_models.dart';
import 'forex_service.dart';

class GoldAmmSection extends StatefulWidget {
  const GoldAmmSection({super.key, required this.forexService});

  final ForexService forexService;

  @override
  State<GoldAmmSection> createState() => _GoldAmmSectionState();
}

class _GoldAmmSectionState extends State<GoldAmmSection> {
  bool _loading = true;
  String? _error;
  List<GoldAmmPool> _pools = const [];
  GoldBalance _goldBalance = const GoldBalance(balance: 0, blockedInPools: 0, availableBalance: 0);

  String _direction = 'FIAT_TO_GOLD';
  String? _swapCurrencyCode;
  final _swapAmountController = TextEditingController(text: '100');
  GoldAmmSwapQuote? _quote;
  bool _quoting = false;
  bool _swapping = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _swapAmountController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([widget.forexService.fetchGoldPools(), widget.forexService.fetchMyGoldBalance()]);
      if (!mounted) return;
      final pools = results[0] as List<GoldAmmPool>;
      setState(() {
        _pools = pools;
        _goldBalance = results[1] as GoldBalance;
        _swapCurrencyCode ??= pools.isNotEmpty ? pools.first.currencyCode : null;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the gold market. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _getQuote() async {
    final currencyCode = _swapCurrencyCode;
    final amount = double.tryParse(_swapAmountController.text);
    if (currencyCode == null || amount == null || amount <= 0) return;
    setState(() => _quoting = true);
    try {
      final quote = await widget.forexService.fetchGoldSwapQuote(direction: _direction, currencyCode: currencyCode, amount: amount);
      if (mounted) setState(() => _quote = quote);
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not get a gold quote.')));
    } finally {
      if (mounted) setState(() => _quoting = false);
    }
  }

  Future<void> _confirmSwap() async {
    final quote = _quote;
    if (quote == null) return;
    setState(() => _swapping = true);
    try {
      // 1% slippage tolerance below the quoted output, matching the web's
      // default acceptance band for the gold AMM swap confirmation.
      await widget.forexService.executeGoldSwap(
        direction: quote.direction,
        currencyCode: quote.currencyCode,
        amount: quote.inputAmount,
        minOutputAmount: quote.outputAmount * 0.99,
      );
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Gold swap complete.')));
      setState(() => _quote = null);
      await _load();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Gold swap failed. Please try again.')));
    } finally {
      if (mounted) setState(() => _swapping = false);
    }
  }

  Future<void> _openAddLiquidityDialog(GoldAmmPool pool) async {
    final fiatController = TextEditingController();
    final maxGoldController = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text('Add liquidity · ${pool.currencyCode}'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(controller: fiatController, decoration: const InputDecoration(labelText: 'Fiat amount'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
            TextField(controller: maxGoldController, decoration: const InputDecoration(labelText: 'Max gold amount'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Add')),
        ],
      ),
    );
    if (confirmed != true) return;
    final fiatAmount = double.tryParse(fiatController.text) ?? 0;
    final maxGoldAmount = double.tryParse(maxGoldController.text) ?? 0;
    try {
      await widget.forexService.addGoldLiquidity(poolId: pool.id, fiatAmount: fiatAmount, maxGoldAmount: maxGoldAmount);
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Liquidity added.')));
      await _load();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not add liquidity.')));
    }
  }

  Future<void> _openRemoveLiquidityDialog(GoldAmmPool pool) async {
    final position = pool.myPosition;
    if (position == null) return;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Remove liquidity'),
        content: Text('Withdraw your entire position from the ${pool.currencyCode} pool?'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Remove all')),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await widget.forexService.removeGoldLiquidity(positionId: position.id, shareFraction: 1.0);
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Liquidity removed.')));
      await _load();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not remove liquidity.')));
    }
  }

  Future<void> _openCreatePoolDialog() async {
    final currencyController = TextEditingController();
    final fiatController = TextEditingController();
    final goldController = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Create gold pool'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(controller: currencyController, decoration: const InputDecoration(labelText: 'Currency code')),
            TextField(controller: fiatController, decoration: const InputDecoration(labelText: 'Fiat amount'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
            TextField(controller: goldController, decoration: const InputDecoration(labelText: 'Gold amount'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Create')),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await widget.forexService.createGoldPool(
        currencyCode: currencyController.text.trim().toUpperCase(),
        fiatAmount: double.tryParse(fiatController.text) ?? 0,
        goldAmount: double.tryParse(goldController.text) ?? 0,
      );
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Pool created.')));
      await _load();
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not create the pool.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Column(children: [Text(_error!), const SizedBox(height: 8), OutlinedButton(onPressed: _load, child: const Text('Try again'))]);
    }

    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('My gold', style: theme.textTheme.titleSmall),
                Text('Balance: ${_goldBalance.balance.toStringAsFixed(4)}'),
                Text('Available: ${_goldBalance.availableBalance.toStringAsFixed(4)}'),
                if (_goldBalance.blockedInPools > 0) Text('In pools: ${_goldBalance.blockedInPools.toStringAsFixed(4)}'),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Text('Swap', style: theme.textTheme.titleSmall),
        Row(
          children: [
            Expanded(child: ChoiceChip(label: const Text('Fiat → Gold'), selected: _direction == 'FIAT_TO_GOLD', onSelected: (_) => setState(() { _direction = 'FIAT_TO_GOLD'; _quote = null; }))),
            const SizedBox(width: 8),
            Expanded(child: ChoiceChip(label: const Text('Gold → Fiat'), selected: _direction == 'GOLD_TO_FIAT', onSelected: (_) => setState(() { _direction = 'GOLD_TO_FIAT'; _quote = null; }))),
          ],
        ),
        const SizedBox(height: 8),
        if (_pools.isNotEmpty)
          DropdownButtonFormField<String>(
            key: const Key('gold-swap-currency'),
            initialValue: _swapCurrencyCode,
            decoration: const InputDecoration(labelText: 'Currency'),
            items: [for (final pool in _pools) DropdownMenuItem(value: pool.currencyCode, child: Text(pool.currencyCode))],
            onChanged: (value) => setState(() { _swapCurrencyCode = value; _quote = null; }),
          ),
        TextField(
          controller: _swapAmountController,
          decoration: const InputDecoration(labelText: 'Amount'),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          onChanged: (_) => setState(() => _quote = null),
        ),
        const SizedBox(height: 8),
        OutlinedButton(onPressed: _quoting ? null : _getQuote, child: Text(_quoting ? 'Getting quote…' : 'Get quote')),
        if (_quote != null) ...[
          const SizedBox(height: 8),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('You receive: ${_quote!.outputAmount.toStringAsFixed(4)}'),
                  Text('Fee: ${_quote!.feeAmount.toStringAsFixed(4)}'),
                  Text('Slippage: ${_quote!.slippagePercent.toStringAsFixed(2)}%'),
                  const SizedBox(height: 8),
                  FilledButton(onPressed: _swapping ? null : _confirmSwap, child: Text(_swapping ? 'Swapping…' : 'Confirm swap')),
                ],
              ),
            ),
          ),
        ],
        const SizedBox(height: 16),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text('Pools', style: theme.textTheme.titleMedium),
            TextButton(onPressed: _openCreatePoolDialog, child: const Text('New pool')),
          ],
        ),
        if (_pools.isEmpty) const Text('No gold pools exist yet.'),
        for (final pool in _pools)
          Card(
            key: ValueKey('gold-pool-${pool.id}'),
            margin: const EdgeInsets.only(bottom: 8),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(pool.currencyCode, style: theme.textTheme.titleSmall),
                  Text('Fiat reserve: ${pool.fiatReserve.toStringAsFixed(2)} · Gold reserve: ${pool.goldReserve.toStringAsFixed(4)}'),
                  Text('Implied price: ${pool.impliedGoldPrice.toStringAsFixed(2)}'),
                  if (pool.myPosition != null) Text('My share: ${pool.myPosition!.sharePercent.toStringAsFixed(2)}%'),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: [
                      OutlinedButton(onPressed: () => _openAddLiquidityDialog(pool), child: const Text('Add liquidity')),
                      if (pool.myPosition != null)
                        OutlinedButton(onPressed: () => _openRemoveLiquidityDialog(pool), child: const Text('Remove liquidity')),
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
