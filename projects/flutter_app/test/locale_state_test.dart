import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/in_memory_selected_locale_storage.dart';

void main() {
  group('LocaleState', () {
    test('defaults to English before any selection is made', () {
      final state = LocaleState(storage: InMemorySelectedLocaleStorage());

      expect(state.languageCode, 'en');
      expect(state.locale.languageCode, 'en');
    });

    test('setLanguage updates the locale and persists the choice', () async {
      final storage = InMemorySelectedLocaleStorage();
      final state = LocaleState(storage: storage);

      await state.setLanguage('sk');

      expect(state.languageCode, 'sk');
      expect(await storage.read(), 'sk');
    });

    test('setLanguage ignores unsupported language codes', () async {
      final state = LocaleState(storage: InMemorySelectedLocaleStorage());

      await state.setLanguage('fr');

      expect(state.languageCode, 'en');
    });

    test('restoreSelection reapplies a previously persisted language', () async {
      final storage = InMemorySelectedLocaleStorage();
      await storage.write('de');
      final state = LocaleState(storage: storage);

      await state.restoreSelection();

      expect(state.languageCode, 'de');
    });

    test('restoreSelection is a no-op when nothing was persisted', () async {
      final state = LocaleState(storage: InMemorySelectedLocaleStorage());

      await state.restoreSelection();

      expect(state.languageCode, 'en');
    });

    test('restoreSelection ignores a persisted but unsupported language code', () async {
      final storage = InMemorySelectedLocaleStorage();
      await storage.write('fr');
      final state = LocaleState(storage: storage);

      await state.restoreSelection();

      expect(state.languageCode, 'en');
    });
  });
}
