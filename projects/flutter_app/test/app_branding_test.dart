import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// The player-visible app name must be "Capitalism" on every platform shell,
/// not the Flutter project name `capitalism_app` (which stays as the
/// executable / package name).
void main() {
  String read(String path) => File(path).readAsStringSync();

  test('Windows window title and version info say Capitalism', () {
    expect(read('windows/runner/main.cpp'), contains('window.Create(L"Capitalism"'));
    final rc = read('windows/runner/Runner.rc');
    expect(rc, contains('VALUE "FileDescription", "Capitalism"'));
    expect(rc, contains('VALUE "ProductName", "Capitalism"'));
  });

  test('web page title and manifest say Capitalism', () {
    final html = read('web/index.html');
    expect(html, contains('<title>Capitalism</title>'));
    expect(html, contains('name="apple-mobile-web-app-title" content="Capitalism"'));
    final manifest = read('web/manifest.json');
    expect(manifest, contains('"name": "Capitalism"'));
    expect(manifest, contains('"short_name": "Capitalism"'));
  });

  test('Android launcher label and iOS bundle names say Capitalism', () {
    expect(read('android/app/src/main/AndroidManifest.xml'), contains('android:label="Capitalism"'));
    final plist = read('ios/Runner/Info.plist');
    expect(plist, isNot(contains('<string>capitalism_app</string>')));
    expect(plist, contains('<string>Capitalism</string>'));
  });
}
