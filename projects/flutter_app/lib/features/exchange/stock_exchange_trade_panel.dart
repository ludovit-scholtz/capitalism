// Inline per-row buy/sell trade panel for the Stock Exchange listing list —
// ported from the expandable row in
// `projects/frontend/src/components/stock/StockMarketListingRow.vue` so
// buying/selling a stock doesn't require navigating to the separate Stock
// Trading screen (`/stock/trade/:companyId`, still linked from the row for
// the fuller price-history/shareholders/order-book view). Trimmed from the
// web: no settlement-account cash preview or company/person context banner
// — those already live on the full trade screen this panel complements.

import 'package:flutter/material.dart';

import 'stock_models.dart';
import 'stock_service.dart';

class StockExchangeTradePanel extends StatefulWidget {
  const StockExchangeTradePanel({super.key, required this.listing, required this.stockService});

  final StockListing listing;
  final StockService stockService;

  @override
  State<StockExchangeTradePanel> createState() => _StockExchangeTradePanelState();
}

class _StockExchangeTradePanelState extends State<StockExchangeTradePanel> {
  bool _expanded = false;
  bool _accountsLoading = false;
  List<Map<String, String>> _bankAccounts = const [];
  String? _selectedAccountId;
  final _quantityController = TextEditingController(text: '1');
  bool _submitting = false;
  String? _feedback;
  bool _feedbackIsError = false;

  @override
  void dispose() {
    _quantityController.dispose();
    super.dispose();
  }

  double get _quantity => double.tryParse(_quantityController.text) ?? 0;

  Future<void> _toggleExpanded() async {
    final expanding = !_expanded;
    setState(() => _expanded = expanding);
    if (expanding && _bankAccounts.isEmpty && !_accountsLoading) {
      setState(() => _accountsLoading = true);
      try {
        final accounts = await widget.stockService.fetchMyBankAccounts();
        if (mounted) {
          setState(() {
            _bankAccounts = accounts;
            _selectedAccountId = accounts.isEmpty ? null : accounts.first['id'];
          });
        }
      } catch (_) {
        // Non-fatal — the trade mutation still works without a
        // pre-selected settlement account (the backend falls back to a
        // sensible default), matching web's tolerant behavior when the
        // accounts fetch is slow/unavailable.
      } finally {
        if (mounted) setState(() => _accountsLoading = false);
      }
    }
  }

  Future<void> _trade({required bool isBuy}) async {
    if (_quantity <= 0 || _submitting) return;
    setState(() {
      _submitting = true;
      _feedback = null;
    });
    try {
      if (isBuy) {
        await widget.stockService.buyShares(
          companyId: widget.listing.companyId,
          shareCount: _quantity,
          bankAccountId: _selectedAccountId,
        );
      } else {
        await widget.stockService.sellShares(
          companyId: widget.listing.companyId,
          shareCount: _quantity,
          bankAccountId: _selectedAccountId,
        );
      }
      if (mounted) {
        setState(() {
          _feedback = isBuy
              ? 'Bought ${_quantity.toStringAsFixed(0)} shares of ${widget.listing.stockSymbol}.'
              : 'Sold ${_quantity.toStringAsFixed(0)} shares of ${widget.listing.stockSymbol}.';
          _feedbackIsError = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _feedback = 'Could not complete this trade.';
          _feedbackIsError = true;
        });
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final listing = widget.listing;
    final estimatedBuyCost = _quantity * listing.askPrice;
    final estimatedSellProceeds = _quantity * listing.bidPrice;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: EdgeInsets.fromLTRB(12, 0, 12, _expanded ? 0 : 12),
          child: Align(
            alignment: Alignment.centerLeft,
            child: TextButton(
              key: ValueKey('trade-toggle-${listing.companyId}'),
              onPressed: _toggleExpanded,
              child: Text(_expanded ? 'Close trade' : 'Trade'),
            ),
          ),
        ),
        if (_expanded)
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Text('Ask: ${listing.askPrice.toStringAsFixed(2)}', style: theme.textTheme.bodySmall?.copyWith(color: Colors.red)),
                    const SizedBox(width: 16),
                    Text('Bid: ${listing.bidPrice.toStringAsFixed(2)}', style: theme.textTheme.bodySmall?.copyWith(color: Colors.green)),
                  ],
                ),
                const SizedBox(height: 8),
                if (_accountsLoading)
                  const Padding(padding: EdgeInsets.symmetric(vertical: 4), child: LinearProgressIndicator())
                else if (_bankAccounts.isNotEmpty)
                  DropdownButtonFormField<String>(
                    key: ValueKey('trade-account-${listing.companyId}'),
                    initialValue: _selectedAccountId,
                    decoration: const InputDecoration(labelText: 'Settlement account'),
                    items: [
                      for (final account in _bankAccounts)
                        DropdownMenuItem(value: account['id'], child: Text('${account['currencyCode']} · ${account['id']!.substring(0, 8)}')),
                    ],
                    onChanged: (value) => setState(() => _selectedAccountId = value),
                  ),
                const SizedBox(height: 8),
                TextField(
                  key: ValueKey('trade-quantity-${listing.companyId}'),
                  controller: _quantityController,
                  decoration: const InputDecoration(labelText: 'Quantity'),
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  onChanged: (_) => setState(() {}),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: FilledButton(
                        key: ValueKey('trade-buy-${listing.companyId}'),
                        onPressed: _submitting ? null : () => _trade(isBuy: true),
                        child: Text('Buy at ${listing.askPrice.toStringAsFixed(2)}'),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: OutlinedButton(
                        key: ValueKey('trade-sell-${listing.companyId}'),
                        onPressed: _submitting ? null : () => _trade(isBuy: false),
                        child: Text('Sell at ${listing.bidPrice.toStringAsFixed(2)}'),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  'Est. cost: ${estimatedBuyCost.toStringAsFixed(2)} · Est. proceeds: ${estimatedSellProceeds.toStringAsFixed(2)}',
                  style: theme.textTheme.bodySmall,
                ),
                if (_feedback != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      _feedback!,
                      style: theme.textTheme.bodySmall?.copyWith(color: _feedbackIsError ? theme.colorScheme.error : Colors.green),
                    ),
                  ),
              ],
            ),
          ),
      ],
    );
  }
}
