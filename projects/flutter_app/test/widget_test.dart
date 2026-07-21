import 'package:flutter_test/flutter_test.dart';

import 'support/app_harness.dart';

void main() {
  testWidgets('app boots to the Home screen', (tester) async {
    await pumpCapitalismApp(tester);

    expect(find.text('Get Started'), findsOneWidget);
  });
}
