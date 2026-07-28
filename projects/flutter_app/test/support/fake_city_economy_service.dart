import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:capitalism_app/features/city/city_economy_models.dart';
import 'package:capitalism_app/features/city/city_economy_service.dart';

class FakeCityEconomyService implements CityEconomyService {
  FakeCityEconomyService({
    this.economyData = const CityEconomyData(economicCycle: null, activeMarketEvents: [], economicHistory: []),
    this.weather,
    this.powerBalance,
    this.economicReport,
    this.economyError,
    this.weatherError,
    this.powerBalanceError,
    this.economicReportError,
  });

  final CityEconomyData economyData;
  final CityWeatherForecast? weather;
  final CityPowerBalance? powerBalance;
  final CityEconomicReportResult? economicReport;
  final Object? economyError;
  final Object? weatherError;
  final Object? powerBalanceError;
  final Object? economicReportError;

  final List<String> calls = [];

  @override
  Future<CityEconomyData> fetchEconomyData(String cityId) async {
    calls.add('fetchEconomyData');
    if (economyError != null) throw economyError!;
    return economyData;
  }

  @override
  Future<CityWeatherForecast?> fetchWeatherForecast(String cityId) async {
    calls.add('fetchWeatherForecast');
    if (weatherError != null) throw weatherError!;
    return weather;
  }

  @override
  Future<CityPowerBalance?> fetchPowerBalance(String cityId) async {
    calls.add('fetchPowerBalance');
    if (powerBalanceError != null) throw powerBalanceError!;
    return powerBalance;
  }

  @override
  Future<CityEconomicReportResult?> fetchEconomicReport(String cityId) async {
    calls.add('fetchEconomicReport');
    if (economicReportError != null) throw economicReportError!;
    return economicReport;
  }
}
