// Ported from `projects/frontend/src/views/ForexExchangeView.vue`.
//
// Deliberately trimmed (documented, not oversights):
// - No Transfer tab — the web's version reuses a generic
//   `BankAccountTransferPanel` that isn't forex-specific.
// - No Gold tab — it wraps a full AMM (quote/swap/create-pool/
//   add-liquidity/remove-liquidity via `goldAmmPools`/`myGoldBalance`/
//   `goldAmmSwapQuote`/`executeGoldAmmSwap`/`addGoldAmmLiquidity`/
//   `createGoldAmmPool`/`removeGoldAmmLiquidity`), a large separate feature
//   deferred to a later pass.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'forex_models.dart';
import 'forex_service.dart';

class ForexExchangeScreen extends StatefulWidget {
  const ForexExchangeScreen({super.key, GraphQlService? graphQlService, ForexService? forexService})
    : _injectedGraphQlService = graphQlService,
      _injectedForexService = forexService;

  final GraphQlService? _injectedGraphQlService;
  final ForexService? _injectedForexService;

  @override
  State<ForexExchangeScreen> createState() => _ForexExchangeScreenState();
}

class _ForexExchangeScreenState extends State<ForexExchangeScreen> {
  late final ForexService _service;

  String _tab = 'swap';
  bool _loading = true;
  String? _error;
  List<FxRate> _rates = const [];
  List<CurrencyBalance> _balances = const [];
  List<ForexTrade> _history = const [];

  String? _fromCurrency;
  String? _toCurrency;
  final _amountController = TextEditingController(text: '100');
  ForexQuote? _quote;
  bool _quoting = false;
  bool _swapping = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedForexService ?? ForexService(graphQlService);
    WidgetsBinding.instance.addPostFrameCallback((_) => _bootstrap());
  }

  @override
  void dispose() {
    _amountController.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    if (!context.read<AuthState>().isAuthenticated) {
      context.go('/login?redirect=%2Fforex');
      return;
    }
    await _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([_service.fetchRates(), _service.fetchBalances(), _service.fetchHistory()]);
      if (!mounted) return;
      final balances = results[1] as List<CurrencyBalance>;
      setState(() {
        _rates = results[0] as List<FxRate>;
        _balances = balances;
        _history = results[2] as List<ForexTrade>;
        _fromCurrency ??= balances.isNotEmpty ? balances.first.currencyCode : 'EUR';
        _toCurrency ??= _rates.isNotEmpty ? _rates.first.quoteCurrencyCode : 'USD';
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the forex exchange. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _requestQuote() async {
    if (_fromCurrency == null || _toCurrency == null) return;
    setState(() => _quoting = true);
    try {
      final quote = await _service.fetchQuote(
        fromCurrencyCode: _fromCurrency!,
        toCurrencyCode: _toCurrency!,
        amount: double.tryParse(_amountController.text) ?? 0,
      );
      if (mounted) setState(() => _quote = quote);
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not get a quote.')));
      }
    } finally {
      if (mounted) setState(() => _quoting = false);
    }
  }

  Future<void> _executeSwap() async {
    if (_fromCurrency == null || _toCurrency == null) return;
    setState(() => _swapping = true);
    try {
      await _service.executeSwap(
        fromCurrencyCode: _fromCurrency!,
        toCurrencyCode: _toCurrency!,
        amount: double.tryParse(_amountController.text) ?? 0,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Swap complete.')));
      }
      setState(() => _quote = null);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Swap failed. Please try again.')));
      }
    } finally {
      if (mounted) setState(() => _swapping = false);
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
              OutlinedButton(onPressed: _load, child: const Text('Try again')),
            ],
          ),
        ),
      );
    }

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Forex Exchange', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(child: ChoiceChip(label: const Text('Swap'), selected: _tab == 'swap', onSelected: (_) => setState(() => _tab = 'swap'))),
            const SizedBox(width: 8),
            Expanded(child: ChoiceChip(label: const Text('Rates'), selected: _tab == 'rates', onSelected: (_) => setState(() => _tab = 'rates'))),
            const SizedBox(width: 8),
            Expanded(child: ChoiceChip(label: const Text('History'), selected: _tab == 'history', onSelected: (_) => setState(() => _tab = 'history'))),
          ],
        ),
        const SizedBox(height: 16),
        if (_tab == 'swap') ..._buildSwapTab() else if (_tab == 'rates') ..._buildRatesTab() else ..._buildHistoryTab(),
      ],
    );
  }

  List<Widget> _buildSwapTab() {
    final currencyCodes = {..._balances.map((b) => b.currencyCode), ..._rates.map((r) => r.quoteCurrencyCode), ..._rates.map((r) => r.baseCurrencyCode)}.toList()..sort();
    return [
      Text('Your balances', style: Theme.of(context).textTheme.titleSmall),
      Wrap(spacing: 8, children: [for (final balance in _balances) Chip(label: Text('${balance.currencySymbol}${balance.balance.toStringAsFixed(2)}'))]),
      const SizedBox(height: 16),
      DropdownButtonFormField<String>(
        initialValue: _fromCurrency,
        decoration: const InputDecoration(labelText: 'From'),
        items: [for (final code in currencyCodes) DropdownMenuItem(value: code, child: Text(code))],
        onChanged: (value) => setState(() {
          _fromCurrency = value;
          _quote = null;
        }),
      ),
      DropdownButtonFormField<String>(
        initialValue: _toCurrency,
        decoration: const InputDecoration(labelText: 'To'),
        items: [for (final code in currencyCodes) DropdownMenuItem(value: code, child: Text(code))],
        onChanged: (value) => setState(() {
          _toCurrency = value;
          _quote = null;
        }),
      ),
      TextField(
        controller: _amountController,
        decoration: const InputDecoration(labelText: 'Amount'),
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
        onChanged: (_) => setState(() => _quote = null),
      ),
      const SizedBox(height: 12),
      OutlinedButton(onPressed: _quoting ? null : _requestQuote, child: Text(_quoting ? 'Getting quote…' : 'Get quote')),
      if (_quote != null) ...[
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('You receive: ${_quote!.toAmount.toStringAsFixed(2)} ${_quote!.toCurrencyCode}'),
                Text('Fee: ${_quote!.feeAmount.toStringAsFixed(2)}'),
                Text('Rate: ${_quote!.rate.toStringAsFixed(4)}'),
                const SizedBox(height: 8),
                FilledButton(onPressed: _swapping ? null : _executeSwap, child: Text(_swapping ? 'Swapping…' : 'Confirm swap')),
              ],
            ),
          ),
        ),
      ],
    ];
  }

  List<Widget> _buildRatesTab() {
    return [
      for (final rate in _rates)
        ListTile(
          title: Text('${rate.baseCurrencyCode} → ${rate.quoteCurrencyCode}'),
          trailing: Text(rate.rate.toStringAsFixed(4)),
        ),
    ];
  }

  List<Widget> _buildHistoryTab() {
    if (_history.isEmpty) return const [Text('No forex trades yet.')];
    return [
      for (final trade in _history)
        ListTile(
          title: Text('${trade.fromAmount.toStringAsFixed(2)} ${trade.fromCurrencyCode} → ${trade.toAmount.toStringAsFixed(2)} ${trade.toCurrencyCode}'),
          subtitle: Text('Rate ${trade.rate.toStringAsFixed(4)}'),
        ),
    ];
  }
}
