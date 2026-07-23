import 'dart:convert';

import 'package:http/http.dart' as http;

import '../auth/auth_state.dart';
import '../config/app_config.dart';
import '../services/app_logger.dart';

/// Mirrors `GraphQLError` in `projects/frontend/src/lib/graphql.ts`: carries
/// the machine-readable `code` from the first error's `extensions.code` (e.g.
/// `LOGIN_THROTTLED`) so callers can distinguish specific failure types.
class GraphQlException implements Exception {
  GraphQlException(this.message, [this.code]);

  final String message;
  final String? code;

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
    String? endpoint,
  }) async {
    final headers = <String, String>{'Content-Type': 'application/json'};
    final token = _authState.token;
    if (token != null && token.isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }

    final opName = _operationName(query);
    // Resolved per-call rather than as a constant default parameter value —
    // AppConfig.graphqlUrl is mutable at runtime once the player picks a
    // game server on GameServerSelectionScreen, and default parameter
    // values in Dart must be compile-time constants.
    final url = endpoint ?? AppConfig.graphqlUrl;

    if (url.isEmpty) {
      AppLogger.instance.warning('$opName -> no game server selected yet', tag: 'GraphQL');
      throw GraphQlException('No game server selected. Pick one from Game Servers or About.');
    }

    final http.Response response;
    try {
      response = await _client.post(
        Uri.parse(url),
        headers: headers,
        body: jsonEncode({'query': query, 'variables': variables ?? <String, dynamic>{}}),
      );
    } catch (e, stackTrace) {
      AppLogger.instance.error('$opName -> request to $url failed', e, stackTrace, 'GraphQL');
      throw GraphQlException('Network error while contacting the server. Please check your connection and try again.');
    }

    if (response.statusCode >= 400) {
      AppLogger.instance.warning(
        '$opName -> HTTP ${response.statusCode} from $url: ${_truncate(response.body)}',
        tag: 'GraphQL',
      );
    }

    final Map<String, dynamic> decoded;
    try {
      decoded = jsonDecode(response.body) as Map<String, dynamic>;
    } catch (e, stackTrace) {
      AppLogger.instance.error(
        '$opName -> could not parse response (HTTP ${response.statusCode}) from $url',
        e,
        stackTrace,
        'GraphQL',
      );
      throw GraphQlException('Received an unexpected response from the server.');
    }

    final errors = decoded['errors'] as List<dynamic>?;
    if (errors != null && errors.isNotEmpty) {
      final firstError = errors.first as Map<String, dynamic>;
      final extensions = firstError['extensions'] as Map<String, dynamic>?;
      final message = errors.map((error) => (error as Map<String, dynamic>)['message']).join('; ');
      AppLogger.instance.error('$opName -> GraphQL error', message, null, 'GraphQL');
      throw GraphQlException(message, extensions?['code'] as String?);
    }

    AppLogger.instance.debug('$opName -> OK', tag: 'GraphQL');
    return decoded['data'] as Map<String, dynamic>? ?? <String, dynamic>{};
  }

  static final RegExp _operationNamePattern = RegExp(r'(query|mutation|subscription)\s+(\w+)');

  /// Best-effort operation name for log readability — never logs full query
  /// text or variables, since variables can carry secrets (e.g. a login
  /// mutation's password).
  String _operationName(String query) {
    final match = _operationNamePattern.firstMatch(query);
    if (match != null) return '${match.group(1)} ${match.group(2)}';
    return 'anonymous operation';
  }

  String _truncate(String body, {int maxLength = 300}) =>
      body.length <= maxLength ? body : '${body.substring(0, maxLength)}…';
}
