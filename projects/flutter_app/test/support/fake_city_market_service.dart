import 'package:capitalism_app/features/city/city_market_models.dart';
import 'package:capitalism_app/features/city/city_market_service.dart';

class FakeCityMarketService implements CityMarketService {
  FakeCityMarketService({this.demandSummary, this.demandError});

  final CityDemandSummary? demandSummary;
  final Object? demandError;

  final List<String> calls = [];

  @override
  Future<CityDemandSummary?> fetchDemandSummary(String cityId, {int topN = 5, int lastNTicks = 100}) async {
    calls.add('fetchDemandSummary');
    if (demandError != null) throw demandError!;
    return demandSummary;
  }
}
