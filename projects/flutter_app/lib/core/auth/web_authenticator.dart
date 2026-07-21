import 'package:flutter_web_auth_2/flutter_web_auth_2.dart';

/// Thin wrapper around [FlutterWebAuth2.authenticate], abstracted so tests
/// can substitute a fake instead of exercising the real platform channel
/// (Custom Tabs / ASWebAuthenticationSession / loopback server, none of
/// which are available under `flutter test`).
abstract class WebAuthenticator {
  Future<String> authenticate({required String url, required String callbackUrlScheme});
}

class FlutterWebAuthenticator implements WebAuthenticator {
  const FlutterWebAuthenticator();

  @override
  Future<String> authenticate({required String url, required String callbackUrlScheme}) {
    return FlutterWebAuth2.authenticate(url: url, callbackUrlScheme: callbackUrlScheme);
  }
}
