import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

const _defaultHomeStatusData = {
  'gameState': {'currentTick': 1234, 'taxRate': 15.0},
  'rankings': [
    {'displayName': 'Alice', 'totalWealthUsd': 50000},
  ],
};

typedef GraphQlResponder = Map<String, dynamic> Function(Map<String, dynamic> requestBody);

/// A [http.Client] fake that dispatches by inspecting the GraphQL `query`
/// string in each request body — the same underlying `http.Client` is
/// threaded through both `HomeScreen` and `LoginScreen` via
/// `createAppRouter(httpClient: ...)`, so one fake needs to answer both.
/// [onLogin]/[onRegister] receive the decoded request body (so a test can
/// assert on `variables.input`) and return the full decoded GraphQL
/// response (`{'data': ...}` or `{'errors': [...]}`).
http.Client fakeAuthGraphQlClient({GraphQlResponder? onLogin, GraphQlResponder? onRegister}) {
  return MockClient((request) async {
    final body = jsonDecode(request.body) as Map<String, dynamic>;
    final query = body['query'] as String? ?? '';

    if (query.contains('HomeStatus')) {
      return http.Response(jsonEncode({'data': _defaultHomeStatusData}), 200);
    }
    if (query.contains('mutation Login') && onLogin != null) {
      return http.Response(jsonEncode(onLogin(body)), 200);
    }
    if (query.contains('mutation Register') && onRegister != null) {
      return http.Response(jsonEncode(onRegister(body)), 200);
    }
    return http.Response(jsonEncode({'data': <String, dynamic>{}}), 200);
  });
}

Map<String, dynamic> graphQlLoginSuccess({String token = 'test-token'}) => {
  'data': {
    'login': {'token': token, 'expiresAtUtc': DateTime.now().toUtc().add(const Duration(hours: 2)).toIso8601String()},
  },
};

Map<String, dynamic> graphQlRegisterSuccess({String token = 'test-token'}) => {
  'data': {
    'register': {
      'token': token,
      'expiresAtUtc': DateTime.now().toUtc().add(const Duration(hours: 2)).toIso8601String(),
    },
  },
};

Map<String, dynamic> graphQlError(String message, String code) => {
  'errors': [
    {
      'message': message,
      'extensions': {'code': code},
    },
  ],
};
