// Ported from `projects/frontend/src/views/StockTradingView.vue`, including
// the price-history chart (via the shared `SparklineChart` widget instead of
// a charting package dependency — same precedent as the web's own
// list-based fallbacks elsewhere) and the position summary panel (avg buy
// price, unrealized P&L, available cash), computed client-side exactly like
// `projects/frontend/src/lib/stockTrading.ts`'s `computeStockPositionSummary`
// (see `StockPositionSummary.compute` in `stock_models.dart`).

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import '../../core/widgets/sparkline_chart.dart';
import 'stock_models.dart';
import 'stock_service.dart';

class StockTradingScreen extends StatefulWidget {
  const StockTradingScreen({
    super.key,
    required this.companyId,
    GraphQlService? graphQlService,
    StockService? stockService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedStockService = stockService;

  final String companyId;
  final GraphQlService? _injectedGraphQlService;
  final StockService? _injectedStockService;

  @override
  State<StockTradingScreen> createState() => _StockTradingScreenState();
}

class _StockTradingScreenState extends State<StockTradingScreen> {
  late final StockService _service;

  bool _loading = true;
  String? _error;
  StockListing? _listing;
  OrderBook? _orderBook;
  List<StockTradeRecord> _tradeHistory = const [];
  CompanyShareholders? _shareholders;
  List<OpenOrder> _openOrders = const [];
  List<StockPriceHistoryPoint> _priceHistory = const [];
  PersonAccountStockSummary? _personAccount;

  final _quantityController = TextEditingController(text: '1');
  final _limitPriceController = TextEditingController();
  String _limitSide = 'BUY';
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedStockService ?? StockService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    _quantityController.dispose();
    _limitPriceController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final listings = await _service.fetchListings();
      final listing = listings.where((l) => l.companyId == widget.companyId).firstOrNull;
      if (listing == null) {
        if (mounted) {
          setState(() {
            _error = 'This company is not publicly traded.';
            _loading = false;
          });
        }
        return;
      }
      final results = await Future.wait([
        _service.fetchOrderBook(listing.stockSymbol),
        _service.fetchTradeHistory(listing.stockSymbol),
        _service.fetchShareholders(widget.companyId),
        _service.fetchMyOpenOrders(),
        _service.fetchPriceHistory(widget.companyId),
        _service.fetchPersonAccountStockSummary(),
      ]);
      if (!mounted) return;
      setState(() {
        _listing = listing;
        _orderBook = results[0] as OrderBook;
        _tradeHistory = results[1] as List<StockTradeRecord>;
        _shareholders = results[2] as CompanyShareholders;
        _openOrders = (results[3] as List<OpenOrder>).where((o) => o.stockSymbol == listing.stockSymbol).toList();
        _priceHistory = results[4] as List<StockPriceHistoryPoint>;
        _personAccount = results[5] as PersonAccountStockSummary;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load this stock. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _marketTrade(bool buy) async {
    final listing = _listing;
    if (listing == null) return;
    final shareCount = double.tryParse(_quantityController.text) ?? 0;
    if (shareCount <= 0) return;
    setState(() => _submitting = true);
    try {
      if (buy) {
        await _service.buyShares(companyId: listing.companyId, shareCount: shareCount);
      } else {
        await _service.sellShares(companyId: listing.companyId, shareCount: shareCount);
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Trade failed. Please try again.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  Future<void> _placeLimitOrder() async {
    final listing = _listing;
    if (listing == null) return;
    final price = double.tryParse(_limitPriceController.text);
    final quantity = int.tryParse(_quantityController.text);
    if (price == null || quantity == null) return;
    setState(() => _submitting = true);
    try {
      await _service.placeLimitOrder(stockSymbol: listing.stockSymbol, side: _limitSide, limitPrice: price, quantity: quantity);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not place the order.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  Future<void> _cancelOrder(OpenOrder order) async {
    try {
      await _service.cancelLimitOrder(order.id);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not cancel the order.')));
      }
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

    final listing = _listing!;
    final theme = Theme.of(context);
    final orderBook = _orderBook!;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('${listing.companyName} (${listing.stockSymbol})', style: theme.textTheme.headlineSmall),
          Text('${listing.sharePrice.toStringAsFixed(2)} · ${listing.dailyChangePercent.toStringAsFixed(1)}%', style: theme.textTheme.titleMedium),
          if (_priceHistory.length >= 2) ...[
            const SizedBox(height: 12),
            SparklineChart(
              key: const Key('stock-price-history-chart'),
              values: _priceHistory.map((p) => p.price).toList(),
              color: listing.dailyChangePercent >= 0 ? Colors.green : Colors.red,
            ),
          ],
          const SizedBox(height: 16),
          _buildPositionCard(theme, listing),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Market order', style: theme.textTheme.titleSmall),
                  const SizedBox(height: 8),
                  TextField(controller: _quantityController, decoration: const InputDecoration(labelText: 'Shares'), keyboardType: TextInputType.number),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Expanded(child: FilledButton(onPressed: _submitting ? null : () => _marketTrade(true), child: const Text('Buy'))),
                      const SizedBox(width: 8),
                      Expanded(child: OutlinedButton(onPressed: _submitting ? null : () => _marketTrade(false), child: const Text('Sell'))),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Limit order', style: theme.textTheme.titleSmall),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Expanded(child: ChoiceChip(label: const Text('Buy'), selected: _limitSide == 'BUY', onSelected: (_) => setState(() => _limitSide = 'BUY'))),
                      const SizedBox(width: 8),
                      Expanded(child: ChoiceChip(label: const Text('Sell'), selected: _limitSide == 'SELL', onSelected: (_) => setState(() => _limitSide = 'SELL'))),
                    ],
                  ),
                  const SizedBox(height: 8),
                  TextField(controller: _limitPriceController, decoration: const InputDecoration(labelText: 'Limit price'), keyboardType: const TextInputType.numberWithOptions(decimal: true)),
                  const SizedBox(height: 8),
                  FilledButton(onPressed: _submitting ? null : _placeLimitOrder, child: const Text('Place order')),
                ],
              ),
            ),
          ),
          if (_openOrders.isNotEmpty) ...[
            const SizedBox(height: 16),
            Text('Your open orders', style: theme.textTheme.titleMedium),
            for (final order in _openOrders)
              ListTile(
                title: Text('${order.side} ${order.remainingQuantity.toStringAsFixed(0)} @ ${order.limitPrice.toStringAsFixed(2)}'),
                trailing: TextButton(onPressed: () => _cancelOrder(order), child: const Text('Cancel')),
              ),
          ],
          const SizedBox(height: 16),
          Text('Order book', style: theme.textTheme.titleMedium),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Bids'),
                    for (final level in orderBook.bids.take(5)) Text('${level.price.toStringAsFixed(2)} × ${level.totalQuantity.toStringAsFixed(0)}'),
                  ],
                ),
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Asks'),
                    for (final level in orderBook.asks.take(5)) Text('${level.price.toStringAsFixed(2)} × ${level.totalQuantity.toStringAsFixed(0)}'),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Text('Recent trades', style: theme.textTheme.titleMedium),
          if (_tradeHistory.isEmpty)
            const Text('No trades yet.')
          else
            for (final trade in _tradeHistory.take(10))
              Text('${trade.price.toStringAsFixed(2)} × ${trade.quantity.toStringAsFixed(0)} (tick ${trade.executedAtTick})'),
          const SizedBox(height: 16),
          Text('Shareholders', style: theme.textTheme.titleMedium),
          if (_shareholders != null)
            for (final holder in _shareholders!.shareholders)
              ListTile(
                dense: true,
                title: Text(holder.holderName),
                trailing: Text('${(holder.ownershipRatio * 100).toStringAsFixed(1)}%'),
              ),
        ],
      ),
    );
  }

  Widget _buildPositionCard(ThemeData theme, StockListing listing) {
    final personAccount = _personAccount;
    final position = StockPositionSummary.compute(
      companyId: listing.companyId,
      currentSharePrice: listing.sharePrice,
      shareholdings: personAccount?.shareholdings ?? const [],
      stockTrades: personAccount?.stockTrades ?? const [],
    );
    final unrealizedPnl = position.unrealizedPnl;

    return Card(
      key: const Key('stock-position-card'),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Your position', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            Text('Shares owned: ${position.sharesOwned.toStringAsFixed(0)}'),
            Text('Market value: ${position.marketValue.toStringAsFixed(2)}'),
            if (position.averageBuyPrice != null) Text('Average buy price: ${position.averageBuyPrice!.toStringAsFixed(2)}'),
            if (unrealizedPnl != null)
              Text(
                'Unrealized P&L: ${unrealizedPnl >= 0 ? '+' : ''}${unrealizedPnl.toStringAsFixed(2)}',
                style: TextStyle(color: unrealizedPnl >= 0 ? Colors.green : Colors.red),
              ),
            if (personAccount != null) Text('Available cash: ${personAccount.availableCash.toStringAsFixed(2)}'),
          ],
        ),
      ),
    );
  }
}
