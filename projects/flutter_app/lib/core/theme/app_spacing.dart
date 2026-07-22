/// Spacing and radius scale shared across the app so screens stop inlining
/// magic numbers for padding/gaps/corner radii. Mirrors the web frontend's
/// 8px rhythm (see `projects/frontend/docs/design-patterns.md`).
class AppSpacing {
  AppSpacing._();

  static const double xs = 4;
  static const double sm = 8;
  static const double md = 16;
  static const double lg = 24;
  static const double xl = 32;
  static const double xxl = 48;
}

class AppRadius {
  AppRadius._();

  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 20;
}
