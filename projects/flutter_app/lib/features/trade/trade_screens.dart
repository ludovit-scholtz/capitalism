// Ported from `projects/frontend/src/views/TradeRoutesView.vue`.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'trade_models.dart';
import 'trade_service.dart';

class TradeRoutesScreen extends StatefulWidget {
  const TradeRoutesScreen({super.key, GraphQlService? graphQlService, TradeService? tradeService})
    : _injectedGraphQlService = graphQlService,
      _injectedTradeService = tradeService;

  final GraphQlService? _injectedGraphQlService;
  final TradeService? _injectedTradeService;

  @override
  State<TradeRoutesScreen> createState() => _TradeRoutesScreenState();
}

class _TradeRoutesScreenState extends State<TradeRoutesScreen> {
  late final TradeService _service;

  bool _loading = true;
  String? _error;
  List<TradeRoute> _routes = const [];
  String _filter = 'all';

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedTradeService ?? TradeService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final routes = await _service.fetchMyTradeRoutes();
      if (!mounted) return;
      setState(() {
        _routes = routes;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load trade routes. Please try again.';
        _loading = false;
      });
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

    final activeCount = _routes.where((r) => r.status == 'SCHEDULED' || r.status == 'IN_TRANSIT').length;
    final filtered = switch (_filter) {
      'active' => _routes.where((r) => r.status == 'SCHEDULED' || r.status == 'IN_TRANSIT').toList(),
      'completed' => _routes.where((r) => r.status == 'COMPLETED').toList(),
      _ => _routes,
    };
    final theme = Theme.of(context);

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Trade Routes', style: theme.textTheme.headlineSmall),
          Text('$activeCount active shipments', style: theme.textTheme.bodyMedium),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: ChoiceChip(label: const Text('All'), selected: _filter == 'all', onSelected: (_) => setState(() => _filter = 'all'))),
              const SizedBox(width: 8),
              Expanded(child: ChoiceChip(label: const Text('Active'), selected: _filter == 'active', onSelected: (_) => setState(() => _filter = 'active'))),
              const SizedBox(width: 8),
              Expanded(child: ChoiceChip(label: const Text('Completed'), selected: _filter == 'completed', onSelected: (_) => setState(() => _filter = 'completed'))),
            ],
          ),
          const SizedBox(height: 16),
          if (filtered.isEmpty)
            const Text('No trade routes to show.')
          else
            for (final route in filtered)
              Card(
                key: ValueKey('route-${route.id}'),
                margin: const EdgeInsets.only(bottom: 8),
                child: ListTile(
                  title: Text('${route.sourceCityName} → ${route.destinationCityName}'),
                  subtitle: Text('${route.itemName} × ${route.quantity.toStringAsFixed(0)} · ${route.sourceBuildingName} → ${route.destinationBuildingName}'),
                  trailing: Chip(label: Text(route.status)),
                ),
              ),
        ],
      ),
    );
  }
}
