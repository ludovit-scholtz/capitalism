// Ported from `OnboardingView.vue`'s inline FX computeds (`cityUsdFxRate`,
// `getProductLocalPrice`, `companyStartingCash`). Lot prices are already
// generated server-side in the city's local currency (see
// `LandService.ComputeBasePrice`/`ComputeAppraisedPrice`, which fold in
// `cityFxRate` at generation time) — only USD-nominal figures defined once
// globally (product base prices, the fixed IPO founder-contribution/
// raise-target amounts) need converting before display or before being
// compared against a local-currency lot price.

import 'package:intl/intl.dart';

class EurFxRate {
  const EurFxRate({required this.currencyCode, required this.rate});

  final String currencyCode;
  final double rate;

  factory EurFxRate.fromJson(Map<String, dynamic> json) =>
      EurFxRate(currencyCode: json['currencyCode'] as String, rate: (json['rate'] as num).toDouble());
}

/// EUR-based FX rate table (units of currency per 1 EUR), matching the
/// `eurFxRates` query.
class OnboardingFxRates {
  const OnboardingFxRates(this.rates);

  final List<EurFxRate> rates;

  static const OnboardingFxRates empty = OnboardingFxRates([]);

  double _eurRateFor(String currencyCode) {
    if (currencyCode == 'EUR') return 1;
    for (final rate in rates) {
      if (rate.currencyCode == currencyCode) return rate.rate;
    }
    return 1;
  }

  /// Multiplier converting a USD-nominal amount into [targetCurrencyCode],
  /// via the EUR cross rate. Mirrors web's `cityUsdFxRate`.
  double usdToTargetRate(String targetCurrencyCode) {
    if (targetCurrencyCode == 'USD') return 1;
    final eurToUsd = _eurRateFor('USD');
    final eurToTarget = _eurRateFor(targetCurrencyCode);
    return eurToUsd > 0 ? eurToTarget / eurToUsd : 1;
  }

  /// Converts a USD-nominal [usdAmount] into [targetCurrencyCode].
  /// [wholeUnits] rounds to the nearest whole unit (mirrors web's
  /// `Math.round` for lump sums like starting cash); otherwise rounds to
  /// cents (mirrors `getProductLocalPrice`).
  double usdToLocal(double usdAmount, String targetCurrencyCode, {bool wholeUnits = false}) {
    final converted = usdAmount * usdToTargetRate(targetCurrencyCode);
    return wholeUnits ? converted.roundToDouble() : (converted * 100).round() / 100;
  }
}

/// Formats an amount already denominated in [currencyCode] — applies
/// currency symbol/decimal-digit formatting only, no conversion. Use for lot
/// prices and company cash figures, which are already local-currency values.
String formatOnboardingCurrency(double localAmount, String currencyCode) =>
    NumberFormat.simpleCurrency(name: currencyCode).format(localAmount);
