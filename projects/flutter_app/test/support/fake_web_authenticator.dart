import 'package:capitalism_app/core/auth/web_authenticator.dart';

/// [WebAuthenticator] fake that hands the generated authorize URL to
/// [responder] and returns whatever callback URL it builds — lets tests
/// drive [BiatecOidcService] end-to-end without a real browser/webview.
class FakeWebAuthenticator implements WebAuthenticator {
  FakeWebAuthenticator(this.responder);

  final String Function(Uri authorizeUrl) responder;

  @override
  Future<String> authenticate({required String url, required String callbackUrlScheme, bool useWebview = true}) async {
    return responder(Uri.parse(url));
  }
}
