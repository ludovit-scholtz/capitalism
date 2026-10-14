import 'package:capitalism_app/core/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('dark theme defines a page transition for every platform (no abrupt default)', () {
    final builders = AppTheme.dark.pageTransitionsTheme.builders;
    for (final platform in [
      TargetPlatform.android,
      TargetPlatform.iOS,
      TargetPlatform.windows,
      TargetPlatform.linux,
      TargetPlatform.macOS
    ]) {
      expect(builders[platform], isNotNull, reason: '$platform');
    }
  });

  test('touch targets: buttons keep a 48dp minimum height', () {
    final style = AppTheme.dark.filledButtonTheme.style!;
    final size = style.minimumSize!.resolve({});
    expect(size!.height, greaterThanOrEqualTo(48));
    final outlined = AppTheme.dark.outlinedButtonTheme.style!.minimumSize!.resolve({});
    expect(outlined!.height, greaterThanOrEqualTo(48));
  });
}
