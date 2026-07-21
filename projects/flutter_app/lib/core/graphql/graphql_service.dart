import 'dart:convert';

import 'package:http/http.dart' as http;

import '../auth/auth_state.dart';
import '../config/app_config.dart';

class GraphQlException implements Exception {
  GraphQlException(this.message);

  final String message;

  @override
  String toString() => 'GraphQlException: $message';
}

/// Thin GraphQL request helper, mirroring `gqlRequest()` in
/// `projects/frontend/src/lib/graphql.ts`, but authenticating with an
/// `Authorization: Bearer` header (see [AuthState]) instead of cookies.
class GraphQlService {
  GraphQlService(this._authState, {http.Client? client}) : _client = client ?? http.Client();

  final AuthState _authState;
  final http.Client _client;

  Future<Map<String, dynamic>> request(
    String query, {
    Map<String, dynamic>? variables,
    String endpoint = AppConfig.graphqlUrl,
  }) async {
    final headers = <String, String>{'Content-Type': 'application/json'};
    final token = _authState.token;
    if (token != null && token.isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }

    final response = await _client.post(
      Uri.parse(endpoint),
      headers: headers,
      body: jsonEncode({'query': query, 'variables': variables ?? <String, dynamic>{}}),
    );

    final decoded = jsonDecode(response.body) as Map<String, dynamic>;
    final errors = decoded['errors'] as List<dynamic>?;
    if (errors != null && errors.isNotEmpty) {
      final message = errors.map((error) => (error as Map<String, dynamic>)['message']).join('; ');
      throw GraphQlException(message);
    }

    return decoded['data'] as Map<String, dynamic>? ?? <String, dynamic>{};
  }
}
