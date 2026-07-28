// Ported from `projects/frontend/src/views/StockExchangeView.vue`, including
// dividend proposal/voting (`proposeDividend`/`voteDividendProposal`),
// company merger (`mergeCompany`), CEO replacement/hostile takeover
// (`replaceCEO`), and the inline per-row expandable buy/sell trade panel
// (`stock_exchange_trade_panel.dart`, ported from
// `StockMarketListingRow.vue`) so trading doesn't require navigating to the
// separate Stock Trading screen — that screen (`/stock/trade/:companyId`,
// still linked from each row) remains the fuller price-history/order-book/
// shareholders view.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'stock_exchange_trade_panel.dart';
import 'stock_models.dart';
import 'stock_service.dart';

class StockExchangeScreen extends StatefulWidget {
  const StockExchangeScreen({super.key, GraphQlService? graphQlService, StockService? stockService})
    : _injectedGraphQlService = graphQlService,
      _injectedStockService = stockService;

  final GraphQlService? _injectedGraphQlService;
  final StockService? _injectedStockService;

  @override
  State<StockExchangeScreen> createState() => _StockExchangeScreenState();
}

class _StockExchangeScreenState extends State<StockExchangeScreen> {
  late final StockService _service;

  bool _loading = true;
  String? _error;
  List<StockListing> _listings = const [];

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedStockService ?? StockService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final listings = await _service.fetchListings();
      if (!mounted) return;
      setState(() {
        _listings = listings;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the stock exchange. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _openDividendsDialog(StockListing listing) async {
    List<DividendProposal> proposals;
    try {
      proposals = await _service.fetchDividendProposals(listing.stockSymbol);
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not load dividend proposals.')));
      }
      return;
    }
    if (!mounted) return;

    final perShareController = TextEditingController(text: '0.50');
    await showDialog<void>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) {
          Future<void> vote(DividendProposal proposal, String choice) async {
            try {
              await _service.voteDividendProposal(proposalId: proposal.id, choice: choice);
              final refreshed = await _service.fetchDividendProposals(listing.stockSymbol);
              proposals = refreshed;
              setDialogState(() {});
            } catch (_) {
              if (dialogContext.mounted) {
                ScaffoldMessenger.of(dialogContext).showSnackBar(const SnackBar(content: Text('Could not cast your vote.')));
              }
            }
          }

          Future<void> propose() async {
            final perShare = double.tryParse(perShareController.text);
            if (perShare == null || perShare <= 0) return;
            try {
              await _service.proposeDividend(stockSymbol: listing.stockSymbol, dividendPerShare: perShare);
              final refreshed = await _service.fetchDividendProposals(listing.stockSymbol);
              proposals = refreshed;
              setDialogState(() {});
            } catch (_) {
              if (dialogContext.mounted) {
                ScaffoldMessenger.of(dialogContext).showSnackBar(const SnackBar(content: Text('Could not propose a dividend.')));
              }
            }
          }

          return AlertDialog(
            title: Text('Dividends · ${listing.stockSymbol}'),
            content: SizedBox(
              width: double.maxFinite,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (proposals.isEmpty) const Text('No dividend proposals yet.'),
                  for (final proposal in proposals)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('${proposal.dividendPerShare.toStringAsFixed(2)}/share · ${proposal.status}'),
                          Text('For: ${proposal.forVotes.toStringAsFixed(0)} · Against: ${proposal.againstVotes.toStringAsFixed(0)}'),
                          if (proposal.isOpenForVoting && proposal.myVoteChoice == null)
                            Row(
                              children: [
                                TextButton(onPressed: () => vote(proposal, 'FOR'), child: const Text('Vote For')),
                                TextButton(onPressed: () => vote(proposal, 'AGAINST'), child: const Text('Vote Against')),
                              ],
                            )
                          else if (proposal.myVoteChoice != null)
                            Text('You voted: ${proposal.myVoteChoice}'),
                        ],
                      ),
                    ),
                  if (listing.canProposeDividend) ...[
                    const Divider(),
                    Text('Propose a new dividend', style: Theme.of(dialogContext).textTheme.titleSmall),
                    TextField(
                      controller: perShareController,
                      decoration: const InputDecoration(labelText: 'Dividend per share'),
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    ),
                    Align(
                      alignment: Alignment.centerRight,
                      child: FilledButton(onPressed: propose, child: const Text('Propose')),
                    ),
                  ],
                ],
              ),
            ),
            actions: [TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('Close'))],
          );
        },
      ),
    );
  }

  Future<void> _claimControl(StockListing listing) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Claim control?'),
        content: Text('Replace the CEO of ${listing.companyName} with yourself? This requires majority ownership.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Claim control')),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      final me = await _service.fetchPersonAccountStockSummary();
      await _service.replaceCeo(companyId: listing.companyId, newCeoPlayerId: me.playerId);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('You are now the CEO of ${listing.companyName}.')));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not claim control of this company.')));
      }
    }
  }

  Future<void> _openMergeDialog(StockListing listing) async {
    final myCompanies = await _service.fetchMyCompanies();
    if (!mounted) return;
    if (myCompanies.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('You need a company to merge into.')));
      return;
    }
    String destinationCompanyId = myCompanies.first['id']!;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: Text('Merge ${listing.companyName}'),
          content: DropdownButtonFormField<String>(
            initialValue: destinationCompanyId,
            decoration: const InputDecoration(labelText: 'Absorb into'),
            items: [for (final company in myCompanies) DropdownMenuItem(value: company['id'], child: Text(company['name']!))],
            onChanged: (value) => setDialogState(() => destinationCompanyId = value ?? destinationCompanyId),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Merge')),
          ],
        ),
      ),
    );
    if (confirmed != true) return;

    try {
      await _service.mergeCompany(targetCompanyId: listing.companyId, destinationCompanyId: destinationCompanyId);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('${listing.companyName} has been merged in.')));
      }
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not merge this company.')));
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

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Stock Exchange', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 16),
          if (_listings.isEmpty)
            const Text('No companies are publicly traded yet.')
          else
            for (final listing in _listings)
              Card(
                key: ValueKey('stock-listing-${listing.companyId}'),
                margin: const EdgeInsets.only(bottom: 8),
                child: Column(
                  children: [
                    ListTile(
                      title: Text('${listing.companyName} (${listing.stockSymbol})'),
                      subtitle: Text(
                        [if (listing.primaryCityName != null) listing.primaryCityName!, if (listing.primaryIndustry != null) listing.primaryIndustry!].join(' · '),
                      ),
                      trailing: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(listing.sharePrice.toStringAsFixed(2)),
                          Text(
                            '${listing.dailyChangePercent >= 0 ? '+' : ''}${listing.dailyChangePercent.toStringAsFixed(1)}%',
                            style: TextStyle(color: listing.dailyChangePercent >= 0 ? Colors.green : Colors.red),
                          ),
                        ],
                      ),
                      onTap: () => context.go('/stock/trade/${listing.companyId}'),
                    ),
                    StockExchangeTradePanel(listing: listing, stockService: _service),
                    if (listing.canProposeDividend || listing.canClaimControl || listing.canMerge)
                      Padding(
                        padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
                        child: Wrap(
                          spacing: 8,
                          children: [
                            OutlinedButton(onPressed: () => _openDividendsDialog(listing), child: const Text('Dividends')),
                            if (listing.canClaimControl)
                              OutlinedButton(onPressed: () => _claimControl(listing), child: const Text('Claim control')),
                            if (listing.canMerge)
                              OutlinedButton(onPressed: () => _openMergeDialog(listing), child: const Text('Merge')),
                          ],
                        ),
                      ),
                  ],
                ),
              ),
        ],
      ),
    );
  }
}
