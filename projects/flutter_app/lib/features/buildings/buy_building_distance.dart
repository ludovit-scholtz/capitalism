// Ported from `projects/frontend/src/lib/globalExchange.ts`'s
// `computeDistanceKm` (haversine, mirroring the backend's
// `GlobalExchangeCalculator.ComputeDistanceKm`) and
// `projects/frontend/src/lib/buyBuildingMap.ts`'s `nearestBuildingsForLot`.
// Distinct from onboarding's cruder `approxDistanceKm` — Buy Building is the
// one screen on web that uses the real haversine formula for its
// distance-to-existing-buildings feature, and this port keeps that
// distinction rather than unifying the two (web itself is inconsistent here).

import 'dart:math' as math;

const double _earthRadiusKm = 6371;

double _degToRad(double deg) => deg * math.pi / 180;

/// Great-circle distance between two lat/lng points, in kilometers.
double computeDistanceKm(double lat1, double lon1, double lat2, double lon2) {
  final dLat = _degToRad(lat2 - lat1);
  final dLon = _degToRad(lon2 - lon1);
  final a =
      math.sin(dLat / 2) * math.sin(dLat / 2) +
      math.cos(_degToRad(lat1)) * math.cos(_degToRad(lat2)) * math.sin(dLon / 2) * math.sin(dLon / 2);
  final c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a));
  return _earthRadiusKm * c;
}

/// A building candidate with a computed distance to some reference lot.
class NearestBuilding<T> {
  const NearestBuilding({required this.building, required this.distanceKm});

  final T building;
  final double distanceKm;
}

/// Returns the [limit] closest [buildings] to (lotLat, lotLng), ascending by
/// distance. [latOf]/[lngOf] extract coordinates from a building of any
/// shape so this stays reusable across building models.
List<NearestBuilding<T>> nearestBuildingsForLot<T>({
  required double lotLat,
  required double lotLng,
  required List<T> buildings,
  required double Function(T) latOf,
  required double Function(T) lngOf,
  int limit = 3,
}) {
  final withDistance = buildings
      .map((b) => NearestBuilding(building: b, distanceKm: computeDistanceKm(lotLat, lotLng, latOf(b), lngOf(b))))
      .toList()
    ..sort((a, b) => a.distanceKm.compareTo(b.distanceKm));
  return withDistance.take(limit).toList();
}

/// Formats a distance like web's `formatDistanceKm`: `"1.2 km"`.
String formatDistanceKm(double distanceKm) => '${distanceKm.toStringAsFixed(1)} km';
