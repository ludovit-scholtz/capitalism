import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/app_harness.dart';

Future<void> _openSettings(WidgetTester tester) async {
  await tester.tap(find.byIcon(Icons.menu));
  await tester.pumpAndSettle();
  await tester.tap(find.widgetWithText(ListTile, 'Settings'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('Settings screen shows the language picker defaulted to English', (tester) async {
    await pumpCapitalismApp(tester);

    await _openSettings(tester);

    expect(find.text('Language'), findsOneWidget);
    final picker = tester.widget<SegmentedButton<String>>(find.byKey(const Key('settings-language-picker')));
    expect(picker.selected, {'en'});
    expect(find.text('English'), findsOneWidget);
    expect(find.text('Slovenčina'), findsOneWidget);
    expect(find.text('Deutsch'), findsOneWidget);
  });

  testWidgets('Selecting a language updates the picker selection', (tester) async {
    await pumpCapitalismApp(tester);

    await _openSettings(tester);
    await tester.tap(find.text('Slovenčina'));
    await tester.pumpAndSettle();

    final picker = tester.widget<SegmentedButton<String>>(find.byKey(const Key('settings-language-picker')));
    expect(picker.selected, {'sk'});
  });
}
