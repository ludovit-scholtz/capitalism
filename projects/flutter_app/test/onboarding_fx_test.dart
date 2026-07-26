import 'package:capitalism_app/features/onboarding/onboarding_fx.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('OnboardingFxRates.usdToTargetRate', () {
    test('returns 1 for USD (identity)', () {
      const rates = OnboardingFxRates([EurFxRate(currencyCode: 'EUR', rate: 0.92)]);
      expect(rates.usdToTargetRate('USD'), 1);
    });

    test('returns the EUR rate directly when target is EUR', () {
      const rates = OnboardingFxRates([EurFxRate(currencyCode: 'USD', rate: 1.08)]);
      // eurToTarget(EUR) / eurToUsd = 1 / 1.08
      expect(rates.usdToTargetRate('EUR'), closeTo(1 / 1.08, 1e-9));
    });

    test('computes the cross rate via EUR for a non-EUR, non-USD currency', () {
      const rates = OnboardingFxRates([
        EurFxRate(currencyCode: 'USD', rate: 1.08),
        EurFxRate(currencyCode: 'GBP', rate: 0.85),
      ]);
      expect(rates.usdToTargetRate('GBP'), closeTo(0.85 / 1.08, 1e-9));
    });

    test('defaults to identity (rate 1) when the currency is missing from the table', () {
      expect(OnboardingFxRates.empty.usdToTargetRate('EUR'), 1);
    });

    test('treats a missing USD entry as eurToUsd = 1 (uses the target rate directly)', () {
      const rates = OnboardingFxRates([EurFxRate(currencyCode: 'GBP', rate: 0.85)]);
      expect(rates.usdToTargetRate('GBP'), 0.85);
    });

    test('falls back to 1 when the USD rate is explicitly non-positive', () {
      const rates = OnboardingFxRates([EurFxRate(currencyCode: 'USD', rate: 0), EurFxRate(currencyCode: 'GBP', rate: 0.85)]);
      expect(rates.usdToTargetRate('GBP'), 1);
    });
  });

  group('OnboardingFxRates.usdToLocal', () {
    test('passes USD amounts through unconverted', () {
      const rates = OnboardingFxRates([EurFxRate(currencyCode: 'USD', rate: 1.08)]);
      expect(rates.usdToLocal(1234.5, 'USD'), 1234.5);
    });

    test('rounds to cents by default', () {
      const rates = OnboardingFxRates([
        EurFxRate(currencyCode: 'USD', rate: 1.08),
        EurFxRate(currencyCode: 'EUR', rate: 1.0),
      ]);
      // rate = 1/1.08 = 0.925925...; 100 * 0.925925... = 92.59 (rounded to cents)
      expect(rates.usdToLocal(100, 'EUR'), 92.59);
    });

    test('rounds to the nearest whole unit when wholeUnits is true', () {
      const rates = OnboardingFxRates([
        EurFxRate(currencyCode: 'USD', rate: 1.08),
        EurFxRate(currencyCode: 'EUR', rate: 1.0),
      ]);
      expect(rates.usdToLocal(400000, 'EUR', wholeUnits: true), (400000 / 1.08).roundToDouble());
    });

    test('with the empty rate table, is a pure identity conversion', () {
      expect(OnboardingFxRates.empty.usdToLocal(200000, 'EUR', wholeUnits: true), 200000);
    });
  });

  group('formatOnboardingCurrency', () {
    test('formats a USD amount with a dollar sign', () {
      expect(formatOnboardingCurrency(1234.5, 'USD'), contains('1,234.50'));
      expect(formatOnboardingCurrency(1234.5, 'USD'), contains(r'$'));
    });

    test('formats a EUR amount with a euro sign', () {
      expect(formatOnboardingCurrency(1234.5, 'EUR'), contains('€'));
    });
  });
}
