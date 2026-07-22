// Ported from `projects/frontend/src/views/ForexExchangeView.vue`, including
// the Transfer tab (`BankTransferSection`, mirroring
// `BankAccountTransferPanel.vue`), the Gold tab (`GoldAmmSection`, the full
// AMM: quote/swap/create-pool/add-liquidity/remove-liquidity), the
// rate-history chart (via the shared `SparklineChart` widget), the
// commodity-shock event banner (`getActiveMarketEvents`), and slippage
// presets + a quote-countdown timer on the Swap tab.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/widgets/sparkline_chart.dart';
import 'bank_transfer_section.dart';
import 'forex_models.dart';
import 'forex_service.dart';
import 'gold_amm_section.dart';

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
  List<MarketEvent> _marketEvents = const [];

  String? _fromCurrency;
  String? _toCurrency;
  final _amountController = TextEditingController(text: '100');
  ForexQuote? _quote;
  bool _quoting = false;
  bool _swapping = false;
  int _slippageBps = 100;
  Timer? _quoteCountdownTimer;
  int _quoteSecondsRemaining = 0;

  String? _rateHistoryCurrency;
  List<FxRateHistoryPoint> _rateHistory = const [];
  bool _rateHistoryLoading = false;

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
    _quoteCountdownTimer?.cancel();
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
      final results = await Future.wait([
        _service.fetchRates(),
        _service.fetchBalances(),
        _service.fetchHistory(),
        _service.fetchActiveMarketEvents(),
      ]);
      if (!mounted) return;
      final balances = results[1] as List<CurrencyBalance>;
      final rates = results[0] as List<FxRate>;
      setState(() {
        _rates = rates;
        _balances = balances;
        _history = results[2] as List<ForexTrade>;
        _marketEvents = results[3] as List<MarketEvent>;
        _fromCurrency ??= balances.isNotEmpty ? balances.first.currencyCode : 'EUR';
        _toCurrency ??= rates.isNotEmpty ? rates.first.quoteCurrencyCode : 'USD';
        _rateHistoryCurrency ??= rates.isNotEmpty ? rates.first.quoteCurrencyCode : null;
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
      if (mounted) {
        setState(() {
          _quote = quote;
          _quoteSecondsRemaining = quote.quoteExpiresInSeconds;
        });
        _startQuoteCountdown();
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not get a quote.')));
      }
    } finally {
      if (mounted) setState(() => _quoting = false);
    }
  }

  void _startQuoteCountdown() {
    _quoteCountdownTimer?.cancel();
    _quoteCountdownTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() {
        if (_quoteSecondsRemaining <= 1) {
          _quoteSecondsRemaining = 0;
          _quote = null;
          timer.cancel();
        } else {
          _quoteSecondsRemaining -= 1;
        }
      });
    });
  }

  Future<void> _executeSwap() async {
    if (_fromCurrency == null || _toCurrency == null) return;
    setState(() => _swapping = true);
    try {
      await _service.executeSwap(
        fromCurrencyCode: _fromCurrency!,
        toCurrencyCode: _toCurrency!,
        amount: double.tryParse(_amountController.text) ?? 0,
        quoteNonce: _quote?.quoteNonce,
        acceptedSlippageBps: _slippageBps,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Swap complete.')));
      }
      _quoteCountdownTimer?.cancel();
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

  void _selectTab(String tab) {
    setState(() => _tab = tab);
    if (tab == 'rates' && _rateHistory.isEmpty && !_rateHistoryLoading && _rateHistoryCurrency != null) {
      _loadRateHistory(_rateHistoryCurrency!);
    }
  }

  Future<void> _loadRateHistory(String quoteCurrencyCode) async {
    setState(() {
      _rateHistoryCurrency = quoteCurrencyCode;
      _rateHistoryLoading = true;
    });
    try {
      final history = await _service.fetchRateHistory(quoteCurrencyCode);
      if (!mounted) return;
      setState(() {
        _rateHistory = history;
        _rateHistoryLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _rateHistory = const [];
        _rateHistoryLoading = false;
      });
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
        if (_marketEvents.isNotEmpty) ..._buildMarketEventBanners(),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final entry in const {'swap': 'Swap', 'transfer': 'Transfer', 'rates': 'Rates', 'history': 'History', 'gold': 'Gold'}.entries)
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: ChoiceChip(label: Text(entry.value), selected: _tab == entry.key, onSelected: (_) => _selectTab(entry.key)),
                ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        if (_tab == 'swap') ..._buildSwapTab(),
        if (_tab == 'transfer') BankTransferSection(forexService: _service),
        if (_tab == 'rates') ..._buildRatesTab(),
        if (_tab == 'history') ..._buildHistoryTab(),
        if (_tab == 'gold') GoldAmmSection(forexService: _service),
      ],
    );
  }

  List<Widget> _buildMarketEventBanners() {
    return [
      for (final event in _marketEvents)
        Card(
          key: ValueKey('market-event-${event.id}'),
          color: Theme.of(context).colorScheme.tertiaryContainer,
          margin: const EdgeInsets.only(bottom: 8),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(event.title, style: Theme.of(context).textTheme.titleSmall),
                Text(event.description, style: Theme.of(context).textTheme.bodySmall),
                Text('${event.ticksRemaining} ticks remaining', style: Theme.of(context).textTheme.bodySmall),
              ],
            ),
          ),
        ),
      const SizedBox(height: 8),
    ];
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
      Text('Slippage tolerance', style: Theme.of(context).textTheme.labelMedium),
      Wrap(
        spacing: 8,
        children: [
          for (final bps in const [50, 100, 200])
            ChoiceChip(
              key: Key('slippage-$bps'),
              label: Text('${(bps / 100).toStringAsFixed(1)}%'),
              selected: _slippageBps == bps,
              onSelected: (_) => setState(() => _slippageBps = bps),
            ),
        ],
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
                Text('Quote expires in ${_quoteSecondsRemaining}s'),
                const SizedBox(height: 8),
                FilledButton(
                  onPressed: (_swapping || _quoteSecondsRemaining <= 0) ? null : _executeSwap,
                  child: Text(_swapping ? 'Swapping…' : 'Confirm swap'),
                ),
              ],
            ),
          ),
        ),
      ],
    ];
  }

  List<Widget> _buildRatesTab() {
    final quoteCurrencies = _rates.map((r) => r.quoteCurrencyCode).toSet().toList()..sort();
    return [
      for (final rate in _rates)
        ListTile(
          title: Text('${rate.baseCurrencyCode} → ${rate.quoteCurrencyCode}'),
          trailing: Text(rate.rate.toStringAsFixed(4)),
        ),
      if (quoteCurrencies.isNotEmpty) ...[
        const SizedBox(height: 16),
        Text('Rate history', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          key: const Key('rate-history-currency'),
          initialValue: _rateHistoryCurrency,
          decoration: const InputDecoration(labelText: 'Currency'),
          items: [for (final code in quoteCurrencies) DropdownMenuItem(value: code, child: Text(code))],
          onChanged: (value) {
            if (value != null) _loadRateHistory(value);
          },
        ),
        const SizedBox(height: 8),
        if (_rateHistoryLoading)
          const Center(child: CircularProgressIndicator())
        else if (_rateHistory.length >= 2)
          SparklineChart(key: const Key('rate-history-chart'), values: _rateHistory.map((p) => p.midRate).toList())
        else
          const Text('Not enough history yet.'),
      ],
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
