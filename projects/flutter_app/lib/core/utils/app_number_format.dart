/// Locale-aware number/currency formatting, mirroring
/// `projects/frontend/src/lib/currencyFormat.ts` so the same amount reads
/// the same way (grouping, decimal separator, compact suffix) on both
/// clients for a given app language. Compact notation follows `intl`'s
/// standard CLDR abbreviation rules (thousands/millions/billions, up to 2
/// fraction digits with trailing zeros trimmed) rather than a fixed digit
/// count.
library;

import 'package:intl/intl.dart';

import '../i18n/app_locale.dart';

class AppNumberFormat {
  const AppNumberFormat._();

  /// Compact currency string (e.g. "$1.5M", "20,1 mil. Kč") — matches
  /// `formatCompactMoney` on the web.
  static String compactMoney(num amount, {String currencyCode = 'USD', required String languageCode}) {
    if (!amount.isFinite) return '—';
    return NumberFormat.compactSimpleCurrency(
      locale: resolveIntlLocale(languageCode),
      name: currencyCode,
      decimalDigits: 2,
    ).format(amount);
  }

  /// Full (non-abbreviated) currency string with thousands separators — 0
  /// fraction digits for whole numbers, 2 for fractional amounts, matching
  /// `formatMoney` on the web.
  static String money(num amount, {String currencyCode = 'USD', required String languageCode}) {
    if (!amount.isFinite) return '—';
    return NumberFormat.simpleCurrency(
      locale: resolveIntlLocale(languageCode),
      name: currencyCode,
      decimalDigits: _fractionDigitsFor(amount),
    ).format(amount);
  }

  /// Picks [money] or [compactMoney] depending on available character
  /// budget — matches `formatMoneyByFieldSize` on the web.
  static String moneyByFieldSize(
    num amount, {
    String currencyCode = 'USD',
    required String languageCode,
    int? maxChars,
  }) {
    final full = money(amount, currencyCode: currencyCode, languageCode: languageCode);
    if (maxChars == null || maxChars <= 0) return full;
    return full.length > maxChars
        ? compactMoney(amount, currencyCode: currencyCode, languageCode: languageCode)
        : full;
  }

  /// Full precision string with the ISO 4217 code appended, for
  /// tooltips/titles — matches `formatCurrencyTitle` on the web.
  static String currencyTitle(num amount, {String currencyCode = 'EUR', required String languageCode}) {
    if (!amount.isFinite) return '— $currencyCode';
    return '${_decimal(amount, languageCode, _fractionDigitsFor(amount))} $currencyCode';
  }

  /// Plain grouped number, no currency symbol — matches `formatNumber` on
  /// the web.
  static String number(num value, {required String languageCode, int fractionDigits = 0}) {
    if (!value.isFinite) return '—';
    return _decimal(value, languageCode, fractionDigits);
  }

  /// Compact number without a currency symbol (e.g. "1.5M" share counts,
  /// city population) — matches `formatCompactNumber` on the web.
  static String compactNumber(num value, {required String languageCode}) {
    if (!value.isFinite) return '—';
    return NumberFormat.compact(locale: resolveIntlLocale(languageCode)).format(value);
  }

  static int _fractionDigitsFor(num amount) => amount % 1 == 0 ? 0 : 2;

  static String _decimal(num amount, String languageCode, int fractionDigits) {
    return (NumberFormat.decimalPattern(resolveIntlLocale(languageCode))
          ..minimumFractionDigits = fractionDigits
          ..maximumFractionDigits = fractionDigits)
        .format(amount);
  }
}
