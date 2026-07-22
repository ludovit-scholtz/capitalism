import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config/app_config.dart';

/// Thrown by [PasswordResetService]. [code] mirrors the machine-readable
/// `code` field the Master API returns (e.g. `METHOD_NOT_ALLOWED`,
/// `RESET_TOKEN_INVALID_OR_EXPIRED`) — see `MasterApi/Program.cs`.
class PasswordResetException implements Exception {
  PasswordResetException(this.message, [this.code]);

  final String message;
  final String? code;

  @override
  String toString() => 'PasswordResetException: $message';
}

/// REST client for the two password-reset endpoints, mirroring
/// `projects/frontend/src/lib/passwordReset.ts`. These are plain REST POSTs
/// on the Master API, not GraphQL mutations — there are none for either flow
/// (confirmed against `MasterApi/Program.cs`).
class PasswordResetService {
  PasswordResetService({http.Client? client, String? baseUrl})
    : _client = client ?? http.Client(),
      _baseUrl = baseUrl ?? AppConfig.masterApiBaseUrl;

  final http.Client _client;
  final String _baseUrl;

  /// POST /auth/forgot-password — always returns a generic success message
  /// whether or not the account exists (anti-enumeration); throws only on
  /// disabled-password-auth, blank email, or rate-limit responses.
  Future<String> requestReset(String email) async {
    return _post('/auth/forgot-password', {
      'email': email,
    }, fallbackMessage: 'If an account exists, a reset link has been sent.');
  }

  /// POST /auth/reset-password. The server enforces the 8-char minimum and
  /// combines "invalid" and "expired" into one `RESET_TOKEN_INVALID_OR_EXPIRED`
  /// code — there is no way to distinguish them client-side, matching the web.
  Future<String> resetPassword({required String token, required String newPassword}) async {
    return _post('/auth/reset-password', {
      'token': token,
      'newPassword': newPassword,
    }, fallbackMessage: 'Password has been reset successfully.');
  }

  Future<String> _post(String path, Map<String, String> body, {required String fallbackMessage}) async {
    final http.Response response;
    try {
      response = await _client.post(
        Uri.parse('$_baseUrl$path'),
        headers: const {'Content-Type': 'application/json'},
        body: jsonEncode(body),
      );
    } catch (_) {
      throw PasswordResetException('Could not reach the server. Please try again.');
    }

    Map<String, dynamic>? decoded;
    try {
      decoded = jsonDecode(response.body) as Map<String, dynamic>?;
    } catch (_) {
      decoded = null;
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      return (decoded?['message'] as String?) ?? fallbackMessage;
    }

    throw PasswordResetException(
      (decoded?['message'] as String?) ?? 'Request failed (${response.statusCode}).',
      decoded?['code'] as String?,
    );
  }
}
