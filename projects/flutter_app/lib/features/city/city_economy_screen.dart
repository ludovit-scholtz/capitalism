// Ported from `projects/frontend/src/components/cityTabs/CityEconomyTab.vue`
// — the economic-cycle widget (`EconomyCycleWidget.vue`), the weather +
// power-grid planning section (`CityPowerPlanningSection.vue`), and the
// city economic-health indicators panel (`HealthIndicatorsPanel.vue`).
// Trimmed from the web: bar/sparkline charts use plain colored `Container`
// bars instead of SVG paths (same visual information, no charting
// dependency, matching this app's existing convention elsewhere e.g.
// dividend vote-progress bars); the health ring is a `CircularProgressIndicator`
// instead of a hand-drawn SVG ring.

import 'package:flutter/material.dart';

import '../buildings/building_panel_models.dart';
import 'city_economy_models.dart';
import 'city_economy_service.dart';
import 'city_health_indicators_card.dart';
import 'city_tab_base.dart';

class CityEconomyScreen extends CityTabScreen {
  const CityEconomyScreen({super.key, required super.cityId, super.graphQlService, super.cityTabService, this.economyService});

  final CityEconomyService? economyService;

  @override
  State<CityEconomyScreen> createState() => _CityEconomyScreenState();
}

class _CityEconomyScreenState extends CityTabScreenState<CityEconomyScreen> {
  late final CityEconomyService _economyService;

  bool _loading = true;
  String? _error;
  CityEconomyData? _economyData;
  CityWeatherForecast? _weather;
  CityPowerBalance? _powerBalance;
  CityEconomicReportResult? _economicReport;

  @override
  void initState() {
    super.initState();
    _economyService = widget.economyService ?? CityEconomyService(graphQlService);
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _economyService.fetchEconomyData(widget.cityId),
        _economyService.fetchWeatherForecast(widget.cityId),
        _economyService.fetchPowerBalance(widget.cityId),
        _economyService.fetchEconomicReport(widget.cityId),
      ]);
      if (!mounted) return;
      setState(() {
        _economyData = results[0] as CityEconomyData;
        _weather = results[1] as CityWeatherForecast?;
        _powerBalance = results[2] as CityPowerBalance?;
        _economicReport = results[3] as CityEconomicReportResult?;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load the economy dashboard. Please try again.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return buildLoading();
    if (_error != null) return buildError(_error!, _load);

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text('Economy', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 16),
          _EconomicCycleCard(data: _economyData),
          const SizedBox(height: 16),
          _WeatherPowerSection(weather: _weather, powerBalance: _powerBalance),
          const SizedBox(height: 16),
          Text('City health', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          CityHealthIndicatorsCard(report: _economicReport),
        ],
      ),
    );
  }
}

class _EconomicCycleCard extends StatelessWidget {
  const _EconomicCycleCard({required this.data});

  final CityEconomyData? data;

  Color _phaseColor(String phase) {
    switch (phase) {
      case 'EXPANSION':
        return Colors.green;
      case 'PEAK':
        return Colors.amber;
      case 'RECESSION':
        return Colors.red;
      default:
        return Colors.blueGrey;
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cycle = data?.economicCycle;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text('Economic cycle', style: theme.textTheme.titleSmall),
                const Spacer(),
                if (cycle != null)
                  Chip(
                    label: Text(cycle.phase),
                    backgroundColor: _phaseColor(cycle.phase).withValues(alpha: 0.15),
                    labelStyle: TextStyle(color: _phaseColor(cycle.phase), fontWeight: FontWeight.bold),
                  ),
              ],
            ),
            if (cycle == null)
              const Padding(padding: EdgeInsets.only(top: 8), child: Text('No economic cycle data yet.'))
            else ...[
              const SizedBox(height: 12),
              Row(
                children: [
                  const Text('Intensity'),
                  const Spacer(),
                  Text('${cycle.intensityFactor.toStringAsFixed(2)}×', style: theme.textTheme.bodyMedium),
                ],
              ),
              const SizedBox(height: 4),
              ClipRRect(
                borderRadius: BorderRadius.circular(4),
                child: LinearProgressIndicator(
                  value: (cycle.intensityFactor / 1.5).clamp(0.0, 1.0),
                  minHeight: 8,
                  backgroundColor: theme.colorScheme.surfaceContainerHighest,
                ),
              ),
              const SizedBox(height: 8),
              Text('${cycle.ticksRemaining} ticks remaining in this phase', style: theme.textTheme.bodySmall),
              if (data!.activeMarketEvents.isNotEmpty) ...[
                const SizedBox(height: 12),
                for (final event in data!.activeMarketEvents.take(3))
                  Container(
                    key: ValueKey('market-event-${event.id}'),
                    margin: const EdgeInsets.only(bottom: 8),
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      border: Border.all(color: theme.colorScheme.outlineVariant),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(child: Text(event.title, style: theme.textTheme.labelMedium)),
                            Text(
                              '${event.magnitudeMultiplier >= 1 ? '+' : ''}${((event.magnitudeMultiplier - 1) * 100).toStringAsFixed(0)}%',
                              style: theme.textTheme.labelSmall?.copyWith(fontWeight: FontWeight.bold),
                            ),
                          ],
                        ),
                        Text(event.description, style: theme.textTheme.bodySmall),
                      ],
                    ),
                  ),
              ],
              if (data!.economicHistory.isNotEmpty) ...[
                const SizedBox(height: 12),
                Text('History (last 24 of ${data!.economicHistory.length} ticks)', style: theme.textTheme.labelSmall),
                const SizedBox(height: 4),
                SizedBox(
                  height: 48,
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    spacing: 2,
                    children: [
                      for (final point in data!.economicHistory.reversed.take(24).toList().reversed)
                        Expanded(
                          child: Tooltip(
                            message: '${point.phase} ${point.intensityFactor.toStringAsFixed(2)}x',
                            child: FractionallySizedBox(
                              alignment: Alignment.bottomCenter,
                              heightFactor: (point.intensityFactor / 1.5).clamp(0.12, 1.0),
                              child: Container(color: theme.colorScheme.primary.withValues(alpha: 0.8)),
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

class _WeatherPowerSection extends StatelessWidget {
  const _WeatherPowerSection({required this.weather, required this.powerBalance});

  final CityWeatherForecast? weather;
  final CityPowerBalance? powerBalance;

  Color _statusColor(String status) {
    switch (status) {
      case 'CONSTRAINED':
        return Colors.amber;
      case 'CRITICAL':
        return Colors.red;
      default:
        return Colors.green;
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('⚡ Weather & power planning', style: theme.textTheme.titleMedium),
        const SizedBox(height: 8),
        if (weather != null) ...[
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('🌤️ Current conditions', style: theme.textTheme.titleSmall),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Chip(label: Text('☀️ ${weather!.currentSolarPercent.round()}%')),
                      const SizedBox(width: 8),
                      Chip(label: Text('💨 ${weather!.currentWindPercent.round()}%')),
                    ],
                  ),
                  if (weather!.forecast.isNotEmpty) ...[
                    const SizedBox(height: 12),
                    Text('Forecast (next ${weather!.forecast.length.clamp(0, 24)} ticks)', style: theme.textTheme.labelSmall),
                    const SizedBox(height: 4),
                    SizedBox(
                      height: 48,
                      child: Row(
                        spacing: 2,
                        children: [
                          for (final tick in weather!.forecast.take(24))
                            Expanded(
                              child: Tooltip(
                                message: 'Tick ${tick.tick}: ☀️${tick.solarPercent.round()}% 💨${tick.windPercent.round()}%',
                                child: Column(
                                  children: [
                                    Expanded(
                                      child: FractionallySizedBox(
                                        alignment: Alignment.bottomCenter,
                                        heightFactor: (tick.solarPercent / 100).clamp(0.02, 1.0),
                                        child: Container(color: Colors.amber),
                                      ),
                                    ),
                                    const SizedBox(height: 1),
                                    Expanded(
                                      child: FractionallySizedBox(
                                        alignment: Alignment.bottomCenter,
                                        heightFactor: (tick.windPercent / 100).clamp(0.02, 1.0),
                                        child: Container(color: Colors.indigo.shade200),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                        ],
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
        ],
        Card(
          key: const Key('city-power-balance-card'),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('🏭 Power grid planning', style: theme.textTheme.titleSmall),
                const SizedBox(height: 8),
                if (powerBalance == null)
                  const Text('Loading…')
                else ...[
                  Row(
                    children: [
                      Chip(
                        label: Text(powerBalance!.status),
                        backgroundColor: _statusColor(powerBalance!.status).withValues(alpha: 0.15),
                        labelStyle: TextStyle(color: _statusColor(powerBalance!.status), fontWeight: FontWeight.bold),
                      ),
                      if (powerBalance!.powerPlantCount == 0) ...[
                        const SizedBox(width: 8),
                        const Chip(label: Text('Legacy grid')),
                      ],
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text('Supply: ${powerBalance!.totalSupplyMw.toStringAsFixed(1)} MW'),
                  Text('Demand: ${powerBalance!.totalDemandMw.toStringAsFixed(1)} MW'),
                  Text(
                    'Reserve: ${powerBalance!.reserveMw >= 0 ? '+' : ''}${powerBalance!.reserveMw.toStringAsFixed(1)} MW',
                    style: TextStyle(color: powerBalance!.reserveMw >= 0 ? Colors.green : Colors.red),
                  ),
                  const SizedBox(height: 8),
                  Text(_guidanceFor(powerBalance!), style: theme.textTheme.bodySmall),
                ],
              ],
            ),
          ),
        ),
      ],
    );
  }

  String _guidanceFor(CityPowerBalance balance) {
    if (balance.powerPlantCount == 0) {
      return 'This city still runs on the legacy unmetered supply — build a power plant to start managing capacity explicitly.';
    }
    switch (balance.status) {
      case 'CONSTRAINED':
        return 'Reserve capacity is thin — new power-hungry buildings may get throttled. Consider adding generation.';
      case 'CRITICAL':
        return 'The grid is over capacity. Existing buildings may already be power-constrained; add generation urgently.';
      default:
        return 'Supply comfortably covers demand with healthy reserve capacity.';
    }
  }
}

