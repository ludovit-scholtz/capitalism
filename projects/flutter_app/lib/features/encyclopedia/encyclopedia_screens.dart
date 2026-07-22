// Ported from `projects/frontend/src/views/ManufacturingEncyclopediaView.vue`
// and `ResourceDetailView.vue`.
//
// Deliberately trimmed from the web (documented, not oversights):
// - Only the GraphQL-backed "resources-definition" catalog tab is ported.
//   The web's other 5 topic tabs (onboarding-help, factory-layout-help,
//   sales-shop-help, forex-trading-help, stock-exchange-help) render static
//   local guide content with clickable fullscreen images from
//   `encyclopediaGuideData.ts` — no GraphQL, purely illustrative reference
//   material, not core gameplay. Skipped rather than porting a large static
//   content data file.
// - No `showPro` URL-persisted toggle — the Pro-only filter is local
//   widget state instead, since this app doesn't persist query params the
//   way the web router does elsewhere either.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'encyclopedia_models.dart';
import 'encyclopedia_service.dart';

class EncyclopediaScreen extends StatefulWidget {
  const EncyclopediaScreen({super.key, GraphQlService? graphQlService, EncyclopediaService? encyclopediaService})
    : _injectedGraphQlService = graphQlService,
      _injectedEncyclopediaService = encyclopediaService;

  final GraphQlService? _injectedGraphQlService;
  final EncyclopediaService? _injectedEncyclopediaService;

  @override
  State<EncyclopediaScreen> createState() => _EncyclopediaScreenState();
}

class _EncyclopediaScreenState extends State<EncyclopediaScreen> {
  late final EncyclopediaService _service;

  bool _loading = true;
  String? _error;
  List<EncyclopediaEntry> _entries = const [];
  String _search = '';
  String _kindFilter = 'ALL';

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedEncyclopediaService ?? EncyclopediaService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final entries = await _service.fetchAllEntries();
      if (!mounted) return;
      setState(() {
        _entries = entries;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the encyclopedia. Please try again.';
        _loading = false;
      });
    }
  }

  List<EncyclopediaEntry> get _filtered {
    return _entries.where((entry) {
      if (_kindFilter != 'ALL' && entry.kind != _kindFilter) return false;
      if (_search.isEmpty) return true;
      return entry.name.toLowerCase().contains(_search.toLowerCase());
    }).toList();
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

    final resourceCount = _entries.where((e) => e.kind == 'RESOURCE').length;
    final productCount = _entries.where((e) => e.kind == 'PRODUCT').length;
    final filtered = _filtered;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('Manufacturing Encyclopedia', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text('$resourceCount resources · $productCount products', style: Theme.of(context).textTheme.bodyMedium),
        const SizedBox(height: 16),
        TextField(
          decoration: const InputDecoration(labelText: 'Search', prefixIcon: Icon(Icons.search)),
          onChanged: (value) => setState(() => _search = value),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          children: [
            for (final kind in const ['ALL', 'RESOURCE', 'PRODUCT'])
              ChoiceChip(label: Text(kind), selected: _kindFilter == kind, onSelected: (_) => setState(() => _kindFilter = kind)),
          ],
        ),
        const SizedBox(height: 16),
        if (filtered.isEmpty)
          const Text('No entries match your search.')
        else
          for (final entry in filtered)
            Card(
              key: ValueKey('encyclopedia-${entry.slug}'),
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(entry.name),
                subtitle: Text([entry.kind, if (entry.category != null) entry.category!].join(' · ')),
                trailing: entry.isProOnly ? const Chip(label: Text('⭐ Pro')) : null,
                onTap: () => context.go('/encyclopedia/resource/${entry.slug}'),
              ),
            ),
      ],
    );
  }
}

class ResourceDetailScreen extends StatefulWidget {
  const ResourceDetailScreen({
    super.key,
    required this.slug,
    GraphQlService? graphQlService,
    EncyclopediaService? encyclopediaService,
  }) : _injectedGraphQlService = graphQlService,
       _injectedEncyclopediaService = encyclopediaService;

  final String slug;
  final GraphQlService? _injectedGraphQlService;
  final EncyclopediaService? _injectedEncyclopediaService;

  @override
  State<ResourceDetailScreen> createState() => _ResourceDetailScreenState();
}

class _ResourceDetailScreenState extends State<ResourceDetailScreen> {
  late final EncyclopediaService _service;

  bool _loading = true;
  String? _error;
  EncyclopediaResourceDetail? _detail;
  bool _showPro = true;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedEncyclopediaService ?? EncyclopediaService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final detail = await _service.fetchResourceDetail(widget.slug);
      if (!mounted) return;
      setState(() {
        _detail = detail;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load this entry. Please try again.';
        _loading = false;
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

    final detail = _detail;
    if (detail == null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Entry not found.'),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: () => context.go('/encyclopedia'), child: const Text('Back to encyclopedia')),
            ],
          ),
        ),
      );
    }

    final entry = detail.entry;
    final theme = Theme.of(context);
    final producedBy = _showPro ? detail.producedByRecipes : detail.producedByRecipes.where((r) => !r.output.isProOnly).toList();
    final usedIn = _showPro ? detail.usedInRecipes : detail.usedInRecipes.where((r) => !r.output.isProOnly).toList();

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        TextButton(onPressed: () => context.go('/encyclopedia'), child: const Text('← Back to encyclopedia')),
        Row(
          children: [
            Expanded(child: Text(entry.name, style: theme.textTheme.headlineSmall)),
            if (entry.isProOnly) const Chip(label: Text('⭐ Pro')),
          ],
        ),
        const SizedBox(height: 4),
        Wrap(
          spacing: 6,
          children: [
            if (entry.category != null) Chip(label: Text(entry.category!)),
            if (entry.isPerishable) const Chip(label: Text('Perishable')),
            Chip(label: Text(entry.kind)),
          ],
        ),
        if (entry.description != null) ...[
          const SizedBox(height: 8),
          Text(entry.description!, style: theme.textTheme.bodyMedium),
        ],
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Base price: ${entry.basePrice.toStringAsFixed(2)}'),
                if (entry.weightPerUnit != null) Text('Weight per unit: ${entry.weightPerUnit}'),
                if (entry.baseCraftTicks != null) Text('Craft ticks: ${entry.baseCraftTicks}'),
                if (entry.outputQuantity != null) Text('Output quantity: ${entry.outputQuantity}'),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        SwitchListTile(
          title: const Text('Show Pro-only products'),
          value: _showPro,
          onChanged: (value) => setState(() => _showPro = value),
        ),
        const SizedBox(height: 12),
        Text('Produced by', style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        if (producedBy.isEmpty) const Text('Not produced by any recipe.') else for (final recipe in producedBy) _RecipeCard(recipe: recipe),
        const SizedBox(height: 16),
        Text('Used in', style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        if (usedIn.isEmpty) const Text('Not used in any recipe.') else for (final recipe in usedIn) _RecipeCard(recipe: recipe),
      ],
    );
  }
}

class _RecipeCard extends StatelessWidget {
  const _RecipeCard({required this.recipe});

  final EncyclopediaRecipe recipe;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(recipe.recipeName, style: Theme.of(context).textTheme.titleSmall),
            Text(recipe.buildingType, style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 6),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final input in recipe.inputs)
                  ActionChip(
                    label: Text('${input.name} × ${input.quantity}${input.unitSymbol ?? ''}'),
                    onPressed: () => context.go('/encyclopedia/resource/${input.slug}'),
                  ),
                const Icon(Icons.arrow_forward, size: 16),
                ActionChip(
                  label: Text(recipe.output.name),
                  onPressed: () => context.go('/encyclopedia/resource/${recipe.output.slug}'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
