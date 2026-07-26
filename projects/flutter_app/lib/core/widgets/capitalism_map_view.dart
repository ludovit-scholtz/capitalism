// Shared interactive map component — OpenStreetMap raster tiles via
// `flutter_map`, mirroring the web's Leaflet+OSM setup (`leaflet` +
// `L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png')`, used
// independently in `OnboardingLotSelector.vue`, `WorldMapView.vue`,
// `BuyBuildingSteps.vue`, and `CityMapContent.vue`). Built once here and
// reused across the Flutter ports of all four screens, rather than
// duplicating the Leaflet-lifecycle pattern web repeats per-file.
//
// No API key/billing setup needed (unlike `google_maps_flutter`), and
// `flutter_map` is pure-Dart, so it renders under `flutter test` without a
// platform channel — only actual tile *image decoding* needs faking in
// tests (see `test/support/fake_tile_provider.dart`).

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

/// Marker color palette mirroring web's `cityMapHelpers.ts` — kept as named
/// constants so every lot-map screen stays visually consistent rather than
/// each re-deriving its own hex values.
class CapitalismMapColors {
  const CapitalismMapColors._();

  static const Color available = Color(0xFF00C853);
  static const Color selected = Color(0xFF0047FF);
  static const Color ownedByOther = Color(0xFFF97316);
  static const Color ownedByNpc = Color(0xFF6B7280);
  static const Color affordableOnly = Color(0xFFFF6D00);
}

/// A single tappable marker on a [CapitalismMapView].
class CapitalismMapMarker {
  const CapitalismMapMarker({
    required this.id,
    required this.position,
    required this.color,
    this.size = 16,
    this.tooltip,
    this.onTap,
  });

  /// Stable identifier — rendered as `Key('map-marker-$id')` so widget tests
  /// can target a specific marker without pixel-level hit-testing.
  final String id;
  final LatLng position;
  final Color color;
  final double size;
  final String? tooltip;
  final VoidCallback? onTap;
}

class CapitalismMapView extends StatefulWidget {
  const CapitalismMapView({
    super.key,
    required this.markers,
    this.initialCenter,
    this.initialZoom = 13,
    this.flyToTarget,
    this.flyToZoom,
    this.tileProvider,
    this.interactionFlags,
  });

  final List<CapitalismMapMarker> markers;

  /// Initial center/zoom, used only when [markers] is empty (otherwise the
  /// map fits all marker positions on first build, mirroring Leaflet's
  /// `fitBounds(bounds.pad(...))`).
  final LatLng? initialCenter;
  final double initialZoom;

  /// When this changes to a new non-null value, the map animates its center
  /// to it over ~500ms (mirrors Leaflet's `flyTo`). Pass the same value
  /// every build to pan instantly instead (see [flyToZoom] docs) — most
  /// screens want an instant `panTo`; only World Map uses the animation.
  final LatLng? flyToTarget;
  final double? flyToZoom;

  /// Injectable for tests, so widget tests never hit real network tile
  /// servers — see `test/support/fake_tile_provider.dart`.
  final TileProvider? tileProvider;

  /// Raw `InteractiveFlag` bitmask; defaults to `InteractiveFlag.all`.
  final int? interactionFlags;

  @override
  State<CapitalismMapView> createState() => _CapitalismMapViewState();
}

class _CapitalismMapViewState extends State<CapitalismMapView> with TickerProviderStateMixin {
  final MapController _mapController = MapController();
  AnimationController? _flyController;
  LatLng? _lastFlyTarget;

  @override
  void didUpdateWidget(covariant CapitalismMapView oldWidget) {
    super.didUpdateWidget(oldWidget);
    final target = widget.flyToTarget;
    if (target != null && target != _lastFlyTarget) {
      _lastFlyTarget = target;
      _flyTo(target, widget.flyToZoom);
    }
  }

  @override
  void dispose() {
    _flyController?.dispose();
    _mapController.dispose();
    super.dispose();
  }

  void _flyTo(LatLng target, double? zoom) {
    final MapCamera camera;
    try {
      camera = _mapController.camera;
    } catch (_) {
      return; // Map not laid out yet (e.g. first frame) — nothing to animate from.
    }
    final startCenter = camera.center;
    final startZoom = camera.zoom;
    final endZoom = zoom ?? startZoom;

    _flyController?.dispose();
    final controller = AnimationController(vsync: this, duration: const Duration(milliseconds: 500));
    _flyController = controller;
    final curved = CurvedAnimation(parent: controller, curve: Curves.easeInOut);
    curved.addListener(() {
      final t = curved.value;
      final lat = startCenter.latitude + (target.latitude - startCenter.latitude) * t;
      final lng = startCenter.longitude + (target.longitude - startCenter.longitude) * t;
      final z = startZoom + (endZoom - startZoom) * t;
      _mapController.move(LatLng(lat, lng), z);
    });
    controller.forward();
  }

  @override
  Widget build(BuildContext context) {
    final markerPositions = widget.markers.map((m) => m.position).toList();

    return FlutterMap(
      mapController: _mapController,
      options: MapOptions(
        initialCenter: widget.initialCenter ?? (markerPositions.isNotEmpty ? markerPositions.first : const LatLng(0, 0)),
        initialZoom: widget.initialZoom,
        initialCameraFit: markerPositions.isNotEmpty
            ? CameraFit.coordinates(coordinates: markerPositions, padding: const EdgeInsets.all(32), maxZoom: 16)
            : null,
        interactionOptions: InteractionOptions(flags: widget.interactionFlags ?? InteractiveFlag.all),
      ),
      children: [
        TileLayer(
          urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
          userAgentPackageName: 'io.biatec.capitalism',
          tileProvider: widget.tileProvider,
        ),
        MarkerLayer(
          markers: [
            for (final marker in widget.markers)
              Marker(
                key: ValueKey('map-marker-${marker.id}'),
                point: marker.position,
                width: marker.size,
                height: marker.size,
                child: GestureDetector(
                  onTap: marker.onTap,
                  child: Tooltip(
                    message: marker.tooltip ?? '',
                    child: Container(
                      decoration: BoxDecoration(
                        color: marker.color,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.white, width: 2),
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ],
    );
  }
}
