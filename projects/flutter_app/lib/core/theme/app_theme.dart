import 'package:flutter/material.dart';

/// A dark, neon-on-glass "HUD" design system — the whole app is themed from
/// here (colors, typography, component shapes) so individual screens don't
/// need per-widget styling to look cohesive; that's the point of leaning on
/// [ThemeData] rather than one-off `Container`/`BoxDecoration` styling
/// scattered through every screen.
///
/// The web frontend is dark-first (CLAUDE.md: never fall back to
/// `prefers-color-scheme` for the default). Mirror that here with a fixed
/// dark [ThemeMode] rather than [ThemeMode.system] — `light` exists only as
/// a safety-net fallback for API completeness, not because the app is ever
/// meant to run in it.
class AppTheme {
  AppTheme._();

  // Deep-space neon palette.
  static const Color spaceBlack = Color(0xFF05070D);
  static const Color spaceSurface = Color(0xFF0D1220);
  static const Color spaceSurfaceHigh = Color(0xFF141B2E);
  static const Color spaceSurfaceHigher = Color(0xFF1B2438);
  static const Color neonCyan = Color(0xFF00E5FF);
  static const Color neonViolet = Color(0xFF8C5CFF);
  static const Color neonAmber = Color(0xFFFFC857);
  static const Color neonRed = Color(0xFFFF4D6D);
  static const Color neonGreen = Color(0xFF4AF2A1);
  static const Color mutedText = Color(0xFFA7B1C7);

  static final ColorScheme _darkScheme = ColorScheme.fromSeed(
    seedColor: neonCyan,
    brightness: Brightness.dark,
  ).copyWith(
    primary: neonCyan,
    onPrimary: spaceBlack,
    primaryContainer: const Color(0xFF00363D),
    onPrimaryContainer: neonCyan,
    secondary: neonViolet,
    onSecondary: Colors.white,
    secondaryContainer: const Color(0xFF2A1D5C),
    onSecondaryContainer: neonViolet,
    tertiary: neonAmber,
    onTertiary: spaceBlack,
    tertiaryContainer: const Color(0xFF4A3600),
    onTertiaryContainer: neonAmber,
    error: neonRed,
    onError: spaceBlack,
    errorContainer: const Color(0xFF4A0F1E),
    onErrorContainer: neonRed,
    surface: spaceSurface,
    onSurface: Colors.white,
    surfaceContainerLowest: spaceBlack,
    surfaceContainerLow: spaceSurface,
    surfaceContainer: spaceSurfaceHigh,
    surfaceContainerHigh: spaceSurfaceHigher,
    surfaceContainerHighest: const Color(0xFF232D45),
    onSurfaceVariant: mutedText,
    outline: const Color(0xFF2A3552),
    outlineVariant: const Color(0xFF1A2236),
    shadow: Colors.black,
    scrim: Colors.black,
  );

  /// Bundled locally as assets (see `pubspec.yaml` → `flutter.fonts` and
  /// `assets/fonts/`), not fetched at runtime via the `google_fonts`
  /// package's CDN call — that would make every widget test (and the app's
  /// very first frame on a fresh install) depend on network access, and
  /// `flutter_test`'s `HttpClient` always returns 400 anyway, so runtime
  /// fetching would silently fall back to a system font in every test run.
  static const _displayFont = 'Orbitron';
  static const _bodyFont = 'Inter';
  static const _labelFont = 'Rajdhani';

  static TextTheme _textTheme(ColorScheme scheme) {
    TextStyle style(String family, double size, FontWeight weight, {double letterSpacing = 0}) {
      return TextStyle(
        fontFamily: family,
        fontSize: size,
        fontWeight: weight,
        letterSpacing: letterSpacing,
        color: scheme.onSurface,
      );
    }

    return TextTheme(
      displayLarge: style(_displayFont, 57, FontWeight.w700, letterSpacing: 1.4),
      displayMedium: style(_displayFont, 45, FontWeight.w700, letterSpacing: 1.2),
      displaySmall: style(_displayFont, 36, FontWeight.w600, letterSpacing: 1.0),
      headlineLarge: style(_displayFont, 32, FontWeight.w700, letterSpacing: 1.0),
      headlineMedium: style(_displayFont, 28, FontWeight.w600, letterSpacing: 0.8),
      headlineSmall: style(_displayFont, 24, FontWeight.w600, letterSpacing: 0.6),
      titleLarge: style(_labelFont, 22, FontWeight.w600, letterSpacing: 0.3),
      titleMedium: style(_labelFont, 16, FontWeight.w600, letterSpacing: 0.2),
      titleSmall: style(_labelFont, 14, FontWeight.w600, letterSpacing: 0.2),
      bodyLarge: style(_bodyFont, 16, FontWeight.w400),
      bodyMedium: style(_bodyFont, 14, FontWeight.w400),
      bodySmall: style(_bodyFont, 12, FontWeight.w400, letterSpacing: 0).copyWith(color: scheme.onSurfaceVariant),
      labelLarge: style(_labelFont, 15, FontWeight.w700, letterSpacing: 0.8),
      labelMedium: style(_labelFont, 13, FontWeight.w600, letterSpacing: 0.5),
      labelSmall: style(
        _labelFont,
        11,
        FontWeight.w600,
        letterSpacing: 0.4,
      ).copyWith(color: scheme.onSurfaceVariant),
    );
  }

  static ThemeData _themeFrom(ColorScheme scheme) {
    final textTheme = _textTheme(scheme);
    final radius = BorderRadius.circular(16);

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: Colors.transparent,
      textTheme: textTheme,
      splashFactory: InkSparkle.splashFactory,
      visualDensity: VisualDensity.standard,
      appBarTheme: AppBarTheme(
        backgroundColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        surfaceTintColor: Colors.transparent,
        centerTitle: false,
        titleTextStyle: textTheme.titleLarge?.copyWith(color: scheme.onSurface),
        iconTheme: IconThemeData(color: scheme.onSurface),
      ),
      cardTheme: CardThemeData(
        color: scheme.surfaceContainer.withValues(alpha: 0.72),
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: radius,
          side: BorderSide(color: scheme.outline.withValues(alpha: 0.6)),
        ),
      ),
      dividerTheme: DividerThemeData(color: scheme.outlineVariant, space: 24, thickness: 1),
      listTileTheme: ListTileThemeData(
        iconColor: scheme.primary,
        textColor: scheme.onSurface,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: scheme.primary,
          foregroundColor: scheme.onPrimary,
          textStyle: textTheme.labelLarge,
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: scheme.primary,
          textStyle: textTheme.labelLarge,
          side: BorderSide(color: scheme.primary.withValues(alpha: 0.6)),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: scheme.primary,
          textStyle: textTheme.labelLarge,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
      ),
      iconButtonTheme: IconButtonThemeData(style: IconButton.styleFrom(foregroundColor: scheme.onSurface)),
      chipTheme: ChipThemeData(
        backgroundColor: scheme.surfaceContainerHigh,
        side: BorderSide(color: scheme.outline),
        labelStyle: textTheme.labelMedium?.copyWith(color: scheme.onSurface),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surfaceContainer.withValues(alpha: 0.6),
        labelStyle: textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: scheme.outline),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: scheme.outline),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: scheme.primary, width: 1.5),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: scheme.error),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: scheme.surfaceContainer.withValues(alpha: 0.92),
        elevation: 0,
        surfaceTintColor: Colors.transparent,
        indicatorColor: scheme.primary.withValues(alpha: 0.22),
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => textTheme.labelSmall?.copyWith(
            color: states.contains(WidgetState.selected) ? scheme.primary : scheme.onSurfaceVariant,
            fontWeight: states.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w500,
          ),
        ),
        iconTheme: WidgetStateProperty.resolveWith(
          (states) =>
              IconThemeData(color: states.contains(WidgetState.selected) ? scheme.primary : scheme.onSurfaceVariant),
        ),
      ),
      drawerTheme: DrawerThemeData(backgroundColor: scheme.surfaceContainerLowest, surfaceTintColor: Colors.transparent),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: scheme.primary, linearTrackColor: scheme.outlineVariant),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: scheme.surfaceContainerHigh,
        contentTextStyle: textTheme.bodyMedium?.copyWith(color: scheme.onSurface),
        actionTextColor: scheme.primary,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: scheme.primary,
        unselectedLabelColor: scheme.onSurfaceVariant,
        indicatorColor: scheme.primary,
        labelStyle: textTheme.labelLarge,
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: scheme.surfaceContainer,
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: scheme.surfaceContainerHigh,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      ),
    );
  }

  static final ThemeData dark = _themeFrom(_darkScheme);

  static final ThemeData light = _themeFrom(
    ColorScheme.fromSeed(seedColor: neonCyan, brightness: Brightness.light),
  );
}
