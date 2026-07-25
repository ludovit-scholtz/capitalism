import '../config/app_config.dart';

/// Resolves the backend-hosted URL for a resource/product catalog picture.
/// Mirrors `projects/frontend/src/lib/productImages.ts`: artwork lives on the
/// game API (`Api/wwwroot/images/products`) keyed by slug, not bundled with
/// this app, so both clients share one set of pictures.
String catalogImageUrl(String slug) => '${AppConfig.gameApiBaseUrl}/images/products/$slug.svg';

/// Shared placeholder artwork for a slug with no dedicated picture.
String catalogFallbackImageUrl() => catalogImageUrl('fallback');
