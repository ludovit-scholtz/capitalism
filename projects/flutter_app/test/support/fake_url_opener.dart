import 'package:capitalism_app/core/services/url_opener.dart';

/// [UrlOpener] fake that records URLs instead of launching a real browser.
class FakeUrlOpener implements UrlOpener {
  final List<String> openedUrls = [];

  @override
  Future<bool> open(String url) async {
    openedUrls.add(url);
    return true;
  }
}
