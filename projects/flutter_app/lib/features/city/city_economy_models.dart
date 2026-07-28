// Data models for the City Economy tab's dashboards, mirroring
// `projects/frontend/src/components/dashboard/EconomyCycleWidget.vue`,
// `cityMap/CityPowerPlanningSection.vue`, and `cityMap/HealthIndicatorsPanel.vue`.
// GraphQL field names verified against `Api/Types/Query.Economy.cs`,
// `Api/Types/Query.Weather.cs`, and `Api/Types/Query.CityEconomicHealth.cs`.
// `CityPowerBalance` itself lives in `building_panel_models.dart` (already
// used by `BuildingPowerPlantPanel`/`DashboardPowerBalanceChip`) and is
// reused here rather than duplicated.
library;

class EconomicCycleView {
  const EconomicCycleView({
    required this.phase,
    required this.intensityFactor,
    required this.ticksRemaining,
  });

  /// `EXPANSION`, `PEAK`, `RECESSION`, or `TROUGH`.
  final String phase;
  final double intensityFactor;
  final int ticksRemaining;

  factory EconomicCycleView.fromJson(Map<String, dynamic> json) => EconomicCycleView(
    phase: (json['phase'] as String?) ?? 'EXPANSION',
    intensityFactor: (json['intensityFactor'] as num?)?.toDouble() ?? 1,
    ticksRemaining: (json['ticksRemaining'] as num?)?.toInt() ?? 0,
  );
}

class MarketEventView {
  const MarketEventView({
    required this.id,
    required this.title,
    required this.description,
    required this.magnitudeMultiplier,
  });

  final String id;
  final String title;
  final String description;
  final double magnitudeMultiplier;

  factory MarketEventView.fromJson(Map<String, dynamic> json) => MarketEventView(
    id: json['id'] as String,
    title: (json['title'] as String?) ?? '',
    description: (json['description'] as String?) ?? '',
    magnitudeMultiplier: (json['magnitudeMultiplier'] as num?)?.toDouble() ?? 1,
  );
}

class EconomicCycleHistoryPoint {
  const EconomicCycleHistoryPoint({required this.tick, required this.phase, required this.intensityFactor});

  final int tick;
  final String phase;
  final double intensityFactor;

  factory EconomicCycleHistoryPoint.fromJson(Map<String, dynamic> json) => EconomicCycleHistoryPoint(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    phase: (json['phase'] as String?) ?? 'EXPANSION',
    intensityFactor: (json['intensityFactor'] as num?)?.toDouble() ?? 1,
  );
}

class WeatherTickPoint {
  const WeatherTickPoint({required this.tick, required this.windPercent, required this.solarPercent});

  final int tick;
  final double windPercent;
  final double solarPercent;

  factory WeatherTickPoint.fromJson(Map<String, dynamic> json) => WeatherTickPoint(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    windPercent: (json['windPercent'] as num?)?.toDouble() ?? 0,
    solarPercent: (json['solarPercent'] as num?)?.toDouble() ?? 0,
  );
}

class CityWeatherForecast {
  const CityWeatherForecast({required this.currentWindPercent, required this.currentSolarPercent, required this.forecast});

  final double currentWindPercent;
  final double currentSolarPercent;
  final List<WeatherTickPoint> forecast;

  factory CityWeatherForecast.fromJson(Map<String, dynamic> json) => CityWeatherForecast(
    currentWindPercent: (json['currentWindPercent'] as num?)?.toDouble() ?? 0,
    currentSolarPercent: (json['currentSolarPercent'] as num?)?.toDouble() ?? 0,
    forecast: ((json['forecast'] as List<dynamic>?) ?? const [])
        .map((e) => WeatherTickPoint.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

/// One tax-cycle economic health snapshot for a city — matches
/// `CityEconomicReport` (`Api/Data/Entities/CityEconomicReport.cs`).
class CityEconomicReport {
  const CityEconomicReport({
    required this.id,
    required this.taxCycleEnd,
    required this.economicIndex,
    required this.totalSalaries,
    required this.totalPublicRevenue,
    required this.activeCompanies,
    required this.averageProductQuality,
    required this.totalPowerSupply,
    required this.totalPowerConsumption,
  });

  final String id;
  final int taxCycleEnd;
  final double economicIndex;
  final double totalSalaries;
  final double totalPublicRevenue;
  final int activeCompanies;
  final double averageProductQuality;
  final double totalPowerSupply;
  final double totalPowerConsumption;

  factory CityEconomicReport.fromJson(Map<String, dynamic> json) => CityEconomicReport(
    id: json['id'] as String,
    taxCycleEnd: (json['taxCycleEnd'] as num?)?.toInt() ?? 0,
    economicIndex: (json['economicIndex'] as num?)?.toDouble() ?? 0,
    totalSalaries: (json['totalSalaries'] as num?)?.toDouble() ?? 0,
    totalPublicRevenue: (json['totalPublicRevenue'] as num?)?.toDouble() ?? 0,
    activeCompanies: (json['activeCompanies'] as num?)?.toInt() ?? 0,
    averageProductQuality: (json['averageProductQuality'] as num?)?.toDouble() ?? 0,
    totalPowerSupply: (json['totalPowerSupply'] as num?)?.toDouble() ?? 0,
    totalPowerConsumption: (json['totalPowerConsumption'] as num?)?.toDouble() ?? 0,
  );
}

class CityEconomicReportResult {
  const CityEconomicReportResult({required this.latest, required this.history});

  final CityEconomicReport? latest;
  final List<CityEconomicReport> history;

  factory CityEconomicReportResult.fromJson(Map<String, dynamic> json) {
    final latestJson = json['latest'] as Map<String, dynamic>?;
    return CityEconomicReportResult(
      latest: latestJson == null ? null : CityEconomicReport.fromJson(latestJson),
      history: ((json['history'] as List<dynamic>?) ?? const [])
          .map((e) => CityEconomicReport.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}
