// Ported from `projects/frontend/src/views/CitiesView.vue` and
// `WorldMapView.vue`.
//
// Deliberately trimmed from the web (documented, not oversights):
// - No country flag images (`CountryFlag.vue`) — the country code is shown
//   as plain text instead, avoiding a flag-icon asset/package dependency.
// - World Map: the interactive Leaflet map (marker pins, fly-to, tile
//   layer) is replaced by a plain sortable list of cities with the same
//   unlock/progress data, the same simplification precedent set by the
//   Onboarding wizard's list-based lot picker (see `onboarding_steps.dart`)
//   — no mapping library dependency added for one screen.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/graphql/graphql_service.dart';
import 'cities_models.dart';
import 'cities_service.dart';

class CitiesScreen extends StatefulWidget {
  const CitiesScreen({super.key, GraphQlService? graphQlService, CitiesService? citiesService})
    : _injectedGraphQlService = graphQlService,
      _injectedCitiesService = citiesService;

  final GraphQlService? _injectedGraphQlService;
  final CitiesService? _injectedCitiesService;

  @override
  State<CitiesScreen> createState() => _CitiesScreenState();
}

class _CitiesScreenState extends State<CitiesScreen> {
  late final CitiesService _service;

  bool _loading = true;
  String? _error;
  List<City> _cities = const [];

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedCitiesService ?? CitiesService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final cities = await _service.fetchCities();
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load cities. Please try again.';
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

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Cities', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text('Explore every city in the world economy.', style: Theme.of(context).textTheme.bodyMedium),
          const SizedBox(height: 16),
          for (final city in _cities) _CityCard(city: city),
        ],
      ),
    );
  }
}

class _CityCard extends StatelessWidget {
  const _CityCard({required this.city});

  final City city;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      key: ValueKey('city-${city.id}'),
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Chip(label: Text(city.countryCode)),
                const SizedBox(width: 8),
                Expanded(child: Text(city.name, style: theme.textTheme.titleMedium)),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: _MetricTile(label: 'POPULATION', value: formatPopulation(city.population)),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _MetricTile(label: 'BASE SALARY', value: '${city.baseSalaryPerManhour.toStringAsFixed(1)} ${city.currencyCode}/h'),
                ),
              ],
            ),
            if (city.resources.isNotEmpty) ...[
              const SizedBox(height: 12),
              Text('TOP RESOURCES', style: theme.textTheme.labelSmall),
              const SizedBox(height: 6),
              Wrap(
                spacing: 6,
                runSpacing: 6,
                children: [
                  for (final resource in city.topResources)
                    Chip(label: Text('${cityResourceIcon(resource.resourceSlug)} ${(resource.abundance * 100).round()}%')),
                ],
              ),
            ],
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton(
                onPressed: () => context.go('/city/${city.id}'),
                child: const Text('🗺️ View city map'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: theme.textTheme.labelSmall),
          Text(value, style: theme.textTheme.titleSmall),
        ],
      ),
    );
  }
}

class WorldMapScreen extends StatefulWidget {
  const WorldMapScreen({super.key, GraphQlService? graphQlService, CitiesService? citiesService})
    : _injectedGraphQlService = graphQlService,
      _injectedCitiesService = citiesService;

  final GraphQlService? _injectedGraphQlService;
  final CitiesService? _injectedCitiesService;

  @override
  State<WorldMapScreen> createState() => _WorldMapScreenState();
}

class _WorldMapScreenState extends State<WorldMapScreen> {
  late final CitiesService _service;

  bool _loading = true;
  String? _error;
  List<ExpansionCity> _cities = const [];
  String? _selectedId;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = widget._injectedGraphQlService ?? GraphQlService(auth);
    _service = widget._injectedCitiesService ?? CitiesService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final cities = await _service.fetchExpansionCities();
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _selectedId = cities.isNotEmpty ? cities.first.id : null;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the world map. Please try again.';
        _loading = false;
      });
    }
  }

  Future<void> _startExpanding(ExpansionCity city) async {
    final auth = context.read<AuthState>();
    if (!auth.isAuthenticated) {
      if (mounted) context.go('/login');
      return;
    }
    final companyId = await _service.fetchMyFirstCompanyId();
    if (!mounted) return;
    context.go(companyId != null ? '/ledger/$companyId' : '/dashboard');
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

    ExpansionCity? selected;
    for (final city in _cities) {
      if (city.id == _selectedId) {
        selected = city;
        break;
      }
    }

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text('World Map', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text('Expand your empire into new cities.', style: Theme.of(context).textTheme.bodyMedium),
        const SizedBox(height: 16),
        for (final city in _cities)
          Card(
            key: ValueKey('expansion-city-${city.id}'),
            color: city.id == _selectedId ? Theme.of(context).colorScheme.surfaceContainerHigh : null,
            child: ListTile(
              leading: Icon(city.isUnlocked ? Icons.lock_open : Icons.lock_outline),
              title: Text(city.name),
              subtitle: Text(city.currencyCode),
              onTap: () => setState(() => _selectedId = city.id),
            ),
          ),
        if (selected != null) ...[
          const SizedBox(height: 16),
          Builder(
            builder: (context) {
              final city = selected!;
              return _CityDetailCard(
                city: city,
                onGoToCity: () => context.go('/city/${city.id}'),
                onStartExpanding: () => _startExpanding(city),
              );
            },
          ),
        ],
      ],
    );
  }
}

class _CityDetailCard extends StatelessWidget {
  const _CityDetailCard({required this.city, required this.onGoToCity, required this.onStartExpanding});

  final ExpansionCity city;
  final VoidCallback onGoToCity;
  final VoidCallback onStartExpanding;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(city.name, style: theme.textTheme.titleMedium),
            const SizedBox(height: 8),
            Text('Population: ${formatPopulation(city.population)}'),
            Text('Currency: ${city.currencyCode}'),
            Text('Available land: ${city.availableLandPlots}'),
            Text('Competition: ${city.activeCompanyCount} companies'),
            if (city.topResourceName != null) Text('Featured resource: ${city.topResourceName}'),
            if (!city.isUnlocked) ...[
              const SizedBox(height: 12),
              LinearProgressIndicator(value: city.progressPercent / 100),
              const SizedBox(height: 4),
              Text('${city.progressPercent}% toward unlocking'),
            ],
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: city.isUnlocked
                  ? FilledButton(onPressed: onGoToCity, child: const Text('Go to city'))
                  : OutlinedButton(onPressed: onStartExpanding, child: const Text('Start expanding')),
            ),
          ],
        ),
      ),
    );
  }
}
