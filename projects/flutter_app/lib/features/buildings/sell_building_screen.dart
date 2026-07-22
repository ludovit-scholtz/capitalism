// Ported from `projects/frontend/src/views/SellBuildingView.vue`.
// Trimmed: no client-side check that blocks un-listing while a
// missed-payment collateral loan is overdue (`myLoans` isn't loaded here) —
// the server still enforces `BUILDING_LOCKED_AS_COLLATERAL` either way, so
// this only affects how early the error surfaces.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'sell_building_models.dart';
import 'sell_building_service.dart';

class SellBuildingScreen extends StatefulWidget {
  const SellBuildingScreen({
    super.key,
    required this.buildingId,
    GraphQlService? graphQlService,
    SellBuildingService? sellBuildingService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedSellBuildingService = sellBuildingService;

  final String buildingId;
  final GraphQlService? _injectedGraphQlService;
  final SellBuildingService? _injectedSellBuildingService;

  @override
  State<SellBuildingScreen> createState() => _SellBuildingScreenState();
}

class _SellBuildingScreenState extends State<SellBuildingScreen> {
  late final SellBuildingService _service;

  bool _loading = true;
  String? _error;
  SellableBuilding? _building;
  final _priceController = TextEditingController();
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedSellBuildingService ?? SellBuildingService(graphQlService);
    _load();
  }

  @override
  void dispose() {
    _priceController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final building = await _service.fetchBuilding(widget.buildingId);
      if (!mounted) return;
      setState(() {
        _building = building;
        _priceController.text = building?.askingPrice?.toStringAsFixed(0) ?? building?.marketValuation?.totalValue.toStringAsFixed(0) ?? '';
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load this building. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _listForSale() async {
    setState(() => _submitting = true);
    try {
      await _service.setForSale(
        buildingId: widget.buildingId,
        isForSale: true,
        askingPrice: double.tryParse(_priceController.text),
      );
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not update the listing.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  Future<void> _cancelListing() async {
    setState(() => _submitting = true);
    try {
      await _service.setForSale(buildingId: widget.buildingId, isForSale: false);
      await _load();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not cancel the listing.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  Future<void> _confirmDestroy() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Destroy this building?'),
        content: const Text('This cannot be undone. You will receive a partial refund of its market value.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Destroy')),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _submitting = true);
    try {
      await _service.destroyBuilding(widget.buildingId);
      if (mounted) context.go('/dashboard');
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not destroy this building.')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
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

    final building = _building;
    if (building == null) {
      return const Center(child: Text('Building not found.'));
    }

    final theme = Theme.of(context);
    final valuation = building.marketValuation;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(building.name, style: theme.textTheme.headlineSmall),
        Text('${building.type} · Level ${building.level}', style: theme.textTheme.bodyMedium),
        const SizedBox(height: 16),
        if (valuation != null)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Estimated value: ${valuation.totalValue.toStringAsFixed(0)} ${valuation.currencyCode}'),
                  Text('Minimum asking price: ${valuation.minimumSalePrice.toStringAsFixed(0)} ${valuation.currencyCode}'),
                ],
              ),
            ),
          ),
        const SizedBox(height: 16),
        if (building.isCollateralized)
          const Text('This building is locked as loan collateral and cannot be sold or destroyed.')
        else ...[
          Text(building.isForSale ? 'Currently listed for sale' : 'Not listed for sale', style: theme.textTheme.titleMedium),
          const SizedBox(height: 8),
          TextField(
            controller: _priceController,
            decoration: const InputDecoration(labelText: 'Asking price'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              FilledButton(
                onPressed: _submitting ? null : _listForSale,
                child: Text(building.isForSale ? 'Update price' : 'List for sale'),
              ),
              const SizedBox(width: 8),
              if (building.isForSale)
                OutlinedButton(onPressed: _submitting ? null : _cancelListing, child: const Text('Cancel listing')),
            ],
          ),
          const SizedBox(height: 24),
          OutlinedButton(
            style: OutlinedButton.styleFrom(foregroundColor: theme.colorScheme.error),
            onPressed: _submitting ? null : _confirmDestroy,
            child: const Text('Destroy building'),
          ),
        ],
      ],
    );
  }
}
