import 'package:flutter/widgets.dart';
import 'package:flutter_map/flutter_map.dart';

/// A [TileProvider] that returns a tiny transparent in-memory image
/// synchronously, so map widget tests never issue real network requests for
/// OSM tiles (which would be slow, flaky, and rate-limited under CI).
class FakeTileProvider extends TileProvider {
  @override
  ImageProvider getImage(TileCoordinates coordinates, TileLayer options) =>
      MemoryImage(TileProvider.transparentImage);
}
