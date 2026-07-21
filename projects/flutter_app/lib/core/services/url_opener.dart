import 'package:url_launcher/url_launcher.dart';

/// Opens a URL in the platform's external browser/handler. Abstracted so
/// widget tests can substitute a fake instead of exercising url_launcher's
/// platform channel (not available under `flutter test`).
abstract class UrlOpener {
  Future<bool> open(String url);
}

class ExternalUrlOpener implements UrlOpener {
  const ExternalUrlOpener();

  @override
  Future<bool> open(String url) {
    return launchUrl(Uri.parse(url), mode: LaunchMode.externalApplication);
  }
}
