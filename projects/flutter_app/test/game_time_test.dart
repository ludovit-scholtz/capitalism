import 'package:capitalism_app/core/utils/game_time.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';

void main() {
  // `DateFormat` requires locale symbol data to be loaded before formatting
  // any non-'en' locale — see `main.dart`'s own `initializeDateFormatting()`
  // call, which this pure (non-widget) test suite doesn't run.
  setUpAll(() => initializeDateFormatting());


  group('computeInGameTimeUtcFromTick', () {
    test('tick 0 is the game epoch', () {
      expect(computeInGameTimeUtcFromTick(0), DateTime.utc(2000, 1, 1));
    });

    test('each tick adds exactly one hour', () {
      expect(computeInGameTimeUtcFromTick(42), DateTime.utc(2000, 1, 2, 18));
    });

    test('negative ticks clamp to the epoch', () {
      expect(computeInGameTimeUtcFromTick(-5), DateTime.utc(2000, 1, 1));
    });
  });

  group('formatGameTickTime', () {
    test('formats using the requested app language, not a hardcoded locale', () {
      final en = formatGameTickTime(500, 'en');
      final sk = formatGameTickTime(500, 'sk');
      final de = formatGameTickTime(500, 'de');

      expect(en, isNotEmpty);
      expect(sk, isNotEmpty);
      expect(de, isNotEmpty);
      // Different locales format month names/ordering differently.
      expect(en, isNot(sk));
    });

    test('falls back to English for an unsupported language code', () {
      expect(formatGameTickTime(500, 'fr'), formatGameTickTime(500, 'en'));
    });
  });
}
