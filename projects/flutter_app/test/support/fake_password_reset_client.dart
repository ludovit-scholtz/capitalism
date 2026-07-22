import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

typedef RestResponder = http.Response Function(Map<String, dynamic> requestBody);

/// Fake [http.Client] for [PasswordResetService], dispatching by request
/// path since these are plain REST endpoints, not GraphQL.
http.Client fakePasswordResetClient({RestResponder? onForgotPassword, RestResponder? onResetPassword}) {
  return MockClient((request) async {
    final body = jsonDecode(request.body) as Map<String, dynamic>;
    if (request.url.path.endsWith('/auth/forgot-password') && onForgotPassword != null) {
      return onForgotPassword(body);
    }
    if (request.url.path.endsWith('/auth/reset-password') && onResetPassword != null) {
      return onResetPassword(body);
    }
    return http.Response(jsonEncode({'message': 'not found'}), 404);
  });
}

http.Response restSuccess(String message) => http.Response(jsonEncode({'message': message}), 200);

http.Response restError(String message, String code, {int statusCode = 400}) =>
    http.Response(jsonEncode({'message': message, 'code': code}), statusCode);
