import 'package:capitalism_app/features/buildings/buy_building_distance.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('computeDistanceKm', () {
    test('is zero for identical coordinates', () {
      expect(computeDistanceKm(48.15, 17.11, 48.15, 17.11), closeTo(0, 1e-9));
    });

    test('matches a known great-circle distance (Bratislava to Vienna, ~55km)', () {
      final km = computeDistanceKm(48.1486, 17.1077, 48.2082, 16.3738);
      expect(km, closeTo(55, 3)); // real-world distance is ~55km; allow a few km tolerance
    });

    test('is symmetric', () {
      final ab = computeDistanceKm(48.15, 17.11, 48.20, 16.37);
      final ba = computeDistanceKm(48.20, 16.37, 48.15, 17.11);
      expect(ab, closeTo(ba, 1e-9));
    });
  });

  group('nearestBuildingsForLot', () {
    test('sorts ascending by distance and applies the limit', () {
      final result = nearestBuildingsForLot<String>(
        lotLat: 0,
        lotLng: 0,
        buildings: ['far', 'near', 'medium'],
        latOf: (b) => switch (b) { 'near' => 0.01, 'medium' => 0.05, _ => 1.0 },
        lngOf: (_) => 0,
        limit: 2,
      );

      expect(result.map((r) => r.building), ['near', 'medium']);
      expect(result[0].distanceKm, lessThan(result[1].distanceKm));
    });

    test('returns an empty list for no candidate buildings', () {
      final result = nearestBuildingsForLot<String>(
        lotLat: 0,
        lotLng: 0,
        buildings: const [],
        latOf: (_) => 0,
        lngOf: (_) => 0,
      );
      expect(result, isEmpty);
    });
  });

  group('formatDistanceKm', () {
    test('formats to one decimal place with a km suffix', () {
      expect(formatDistanceKm(1.234), '1.2 km');
      expect(formatDistanceKm(0), '0.0 km');
    });
  });
}
