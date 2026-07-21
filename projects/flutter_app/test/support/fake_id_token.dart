import 'dart:convert';

/// Builds an unsigned (test-only) JWT-shaped string. [BiatecOidcService]
/// only decodes the payload segment client-side — signature verification is
/// the backend's job (see README/CLAUDE.md) — so a real signature isn't
/// needed to exercise the client-side state/nonce/issuer/audience checks.
String buildFakeIdToken(Map<String, dynamic> payload) {
  String segment(Object value) => base64Url.encode(utf8.encode(jsonEncode(value))).replaceAll('=', '');
  return '${segment({'alg': 'none'})}.${segment(payload)}.signature';
}
