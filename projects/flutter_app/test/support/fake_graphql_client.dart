import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

/// Fake `http.Client` for [GraphQlService] under `flutter test` — real
/// network access isn't available, so anything reachable via the router
/// (currently just `HomeScreen`'s status query) needs a canned response.
http.Client fakeHomeStatusClient() {
  return MockClient((request) async {
    return http.Response(
      jsonEncode({
        'data': {
          'gameState': {'currentTick': 1234, 'taxRate': 15.0},
          'rankings': [
            {'displayName': 'Alice', 'totalWealthUsd': 50000},
            {'displayName': 'Bob', 'totalWealthUsd': 42000},
          ],
        },
      }),
      200,
    );
  });
}
