import 'package:capitalism_app/core/utils/app_number_format.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('AppNumberFormat.compactMoney', () {
    test('abbreviates large amounts with the currency symbol', () {
      expect(AppNumberFormat.compactMoney(20123456, languageCode: 'en'), '\$20.1M');
      expect(AppNumberFormat.compactMoney(4200, languageCode: 'en'), '\$4.2K');
    });

    test('formats using the requested app language, not a hardcoded locale', () {
      final en = AppNumberFormat.compactMoney(1500000, languageCode: 'en');
      final sk = AppNumberFormat.compactMoney(1500000, languageCode: 'sk');
      expect(en, isNot(sk));
    });

    test('returns an em dash for non-finite values', () {
      expect(AppNumberFormat.compactMoney(double.nan, languageCode: 'en'), '—');
      expect(AppNumberFormat.compactMoney(double.infinity, languageCode: 'en'), '—');
    });
  });

  group('AppNumberFormat.money', () {
    test('uses 0 fraction digits for whole numbers and 2 for fractional amounts', () {
      expect(AppNumberFormat.money(200000, currencyCode: 'EUR', languageCode: 'en'), '€200,000');
      expect(AppNumberFormat.money(25.4, currencyCode: 'EUR', languageCode: 'en'), '€25.40');
    });
  });

  group('AppNumberFormat.moneyByFieldSize', () {
    test('falls back to compact form when the full string exceeds maxChars', () {
      final full = AppNumberFormat.money(1234567, languageCode: 'en');
      final limited = AppNumberFormat.moneyByFieldSize(1234567, languageCode: 'en', maxChars: 5);
      expect(limited.length, lessThan(full.length));
    });

    test('returns the full string when no budget is given', () {
      expect(
        AppNumberFormat.moneyByFieldSize(1234, languageCode: 'en'),
        AppNumberFormat.money(1234, languageCode: 'en'),
      );
    });
  });

  group('AppNumberFormat.number / compactNumber', () {
    test('number formats with locale-correct grouping separators', () {
      expect(AppNumberFormat.number(1234567, languageCode: 'en'), '1,234,567');
      expect(AppNumberFormat.number(1234567, languageCode: 'de'), '1.234.567');
    });

    test('compactNumber abbreviates without a currency symbol', () {
      expect(AppNumberFormat.compactNumber(1500000, languageCode: 'en'), '1.5M');
    });
  });
}
