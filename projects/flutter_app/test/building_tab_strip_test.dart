import 'package:capitalism_app/features/buildings/building_tab_strip.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('BuildingTabStrip', () {
    testWidgets('shows the first tab by default and switches content on tap', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: BuildingTabStrip(
              tabs: [
                BuildingTab(key: 'a', label: 'Tab A', builder: (context) => const Text('Content A')),
                BuildingTab(key: 'b', label: 'Tab B', builder: (context) => const Text('Content B')),
              ],
            ),
          ),
        ),
      );

      expect(find.text('Content A'), findsOneWidget);
      expect(find.text('Content B'), findsNothing);

      await tester.tap(find.byKey(const ValueKey('building-tab-b')));
      await tester.pumpAndSettle();

      expect(find.text('Content A'), findsNothing);
      expect(find.text('Content B'), findsOneWidget);
    });

    testWidgets('honors initialKey', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: BuildingTabStrip(
              initialKey: 'b',
              tabs: [
                BuildingTab(key: 'a', label: 'Tab A', builder: (context) => const Text('Content A')),
                BuildingTab(key: 'b', label: 'Tab B', builder: (context) => const Text('Content B')),
              ],
            ),
          ),
        ),
      );

      expect(find.text('Content B'), findsOneWidget);
      expect(find.text('Content A'), findsNothing);
    });

    testWidgets('falls back to the first tab when the selected key disappears from a shrinking tab set', (tester) async {
      var showThird = true;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: StatefulBuilder(
              builder: (context, setState) => Column(
                children: [
                  BuildingTabStrip(
                    initialKey: 'c',
                    tabs: [
                      BuildingTab(key: 'a', label: 'Tab A', builder: (context) => const Text('Content A')),
                      if (showThird) BuildingTab(key: 'c', label: 'Tab C', builder: (context) => const Text('Content C')),
                    ],
                  ),
                  TextButton(onPressed: () => setState(() => showThird = false), child: const Text('Hide C')),
                ],
              ),
            ),
          ),
        ),
      );

      expect(find.text('Content C'), findsOneWidget);

      await tester.tap(find.text('Hide C'));
      await tester.pumpAndSettle();

      expect(find.text('Content A'), findsOneWidget);
      expect(find.text('Content C'), findsNothing);
    });
  });
}
