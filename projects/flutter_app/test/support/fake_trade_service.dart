import 'package:capitalism_app/features/trade/trade_models.dart';
import 'package:capitalism_app/features/trade/trade_service.dart';

class FakeTradeService implements TradeService {
  FakeTradeService({this.routes = const [], this.fetchError});

  final List<TradeRoute> routes;
  final Object? fetchError;

  final List<String> calls = [];

  @override
  Future<List<TradeRoute>> fetchMyTradeRoutes() async {
    calls.add('fetchMyTradeRoutes');
    if (fetchError != null) throw fetchError!;
    return routes;
  }
}
