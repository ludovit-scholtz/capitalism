import 'package:flutter/material.dart';

/// The web frontend is dark-first (CLAUDE.md: never fall back to
/// `prefers-color-scheme` for the default). Mirror that here with a fixed
/// dark [ThemeMode] rather than [ThemeMode.system].
class AppTheme {
  AppTheme._();

  static const Color seed = Color(0xFF2563EB);

  static final ThemeData light = ThemeData(
    useMaterial3: true,
    brightness: Brightness.light,
    colorSchemeSeed: seed,
  );

  static final ThemeData dark = ThemeData(
    useMaterial3: true,
    brightness: Brightness.dark,
    colorSchemeSeed: seed,
  );
}
