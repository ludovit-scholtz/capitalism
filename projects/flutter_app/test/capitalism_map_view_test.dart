import 'package:capitalism_app/core/widgets/capitalism_map_view.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:latlong2/latlong.dart';

import 'support/fake_tile_provider.dart';

Widget _wrap(Widget child) => MaterialApp(home: Scaffold(body: SizedBox(width: 400, height: 400, child: child)));

void main() {
  testWidgets('renders a marker per entry and fires onTap via its key', (tester) async {
    var tapped = false;
    await tester.pumpWidget(
      _wrap(
        CapitalismMapView(
          tileProvider: FakeTileProvider(),
          markers: [
            CapitalismMapMarker(
              id: 'lot-1',
              position: const LatLng(48.15, 17.11),
              color: CapitalismMapColors.available,
              onTap: () => tapped = true,
            ),
            const CapitalismMapMarker(id: 'lot-2', position: LatLng(48.16, 17.12), color: CapitalismMapColors.selected),
          ],
        ),
      ),
    );
    await tester.pump();

    expect(find.byKey(const Key('map-marker-lot-1')), findsOneWidget);
    expect(find.byKey(const Key('map-marker-lot-2')), findsOneWidget);

    await tester.tap(find.byKey(const Key('map-marker-lot-1')));
    expect(tapped, isTrue);
  });

  testWidgets('renders with zero markers without crashing', (tester) async {
    await tester.pumpWidget(_wrap(CapitalismMapView(tileProvider: FakeTileProvider(), markers: const [])));
    await tester.pump();

    expect(find.byType(CapitalismMapView), findsOneWidget);
  });

  testWidgets('renders with a single marker without crashing (degenerate bounds)', (tester) async {
    await tester.pumpWidget(
      _wrap(
        CapitalismMapView(
          tileProvider: FakeTileProvider(),
          markers: const [CapitalismMapMarker(id: 'only', position: LatLng(48.15, 17.11), color: CapitalismMapColors.selected)],
        ),
      ),
    );
    await tester.pump();

    expect(find.byKey(const Key('map-marker-only')), findsOneWidget);
  });

  testWidgets('changing flyToTarget does not throw', (tester) async {
    Widget build(LatLng? target) => _wrap(
      CapitalismMapView(
        tileProvider: FakeTileProvider(),
        markers: const [CapitalismMapMarker(id: 'a', position: LatLng(48.15, 17.11), color: CapitalismMapColors.available)],
        flyToTarget: target,
      ),
    );

    await tester.pumpWidget(build(null));
    await tester.pump();

    await tester.pumpWidget(build(const LatLng(48.2, 17.2)));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 600));

    expect(tester.takeException(), isNull);
  });
}
