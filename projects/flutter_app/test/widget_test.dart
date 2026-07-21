import 'package:capitalism_app/app.dart';
import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/in_memory_token_storage.dart';

void main() {
  testWidgets('app boots to the Home screen', (tester) async {
    await tester.pumpWidget(
      ChangeNotifierProvider<AuthState>(
        create: (_) => AuthState(storage: InMemoryTokenStorage()),
        child: CapitalismApp(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Home'), findsWidgets);
  });
}
