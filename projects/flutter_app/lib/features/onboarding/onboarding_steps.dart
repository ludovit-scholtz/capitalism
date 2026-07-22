import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';

import '../../core/theme/app_icons.dart';
import 'onboarding_models.dart';

/// Web's `OnboardingLotSelector` uses this same simplified approximation
/// (not true haversine) for both city-center and factory-to-shop distance.
double approxDistanceKm(double lat1, double lon1, double lat2, double lon2) {
  final dLat = lat1 - lat2;
  final dLon = lon1 - lon2;
  return math.sqrt(dLat * dLat + dLon * dLon) * 111;
}

/// Simplified stand-in for the web's `getRecommendedFactoryLotIds`/
/// `getRecommendedShopLotIds` (district-name-regex + population-index
/// heuristics): the two cheapest unowned suitable lots, sorted ascending by
/// price. Good enough to reproduce the "recommended badge, pre-selected"
/// behavior without porting the exact district-matching rules.
List<String> recommendedLotIds(List<CityLot> lots, String buildingType) {
  final candidates = lots.where((l) => !l.isOwned && l.suitableFor(buildingType)).toList()
    ..sort((a, b) => a.price.compareTo(b.price));
  return candidates.take(2).map((l) => l.id).toList();
}

class OnboardingStepScaffold extends StatelessWidget {
  const OnboardingStepScaffold({
    super.key,
    required this.title,
    required this.subtitle,
    required this.child,
    this.error,
  });

  final String title;
  final String subtitle;
  final Widget child;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(title, style: theme.textTheme.headlineSmall),
          const SizedBox(height: 4),
          Text(subtitle, style: theme.textTheme.bodyMedium),
          if (error != null) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: theme.colorScheme.errorContainer,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(error!, style: TextStyle(color: theme.colorScheme.onErrorContainer)),
            ),
          ],
          const SizedBox(height: 16),
          child,
        ],
      ),
    );
  }
}

class OnboardingCityStep extends StatelessWidget {
  const OnboardingCityStep({super.key, required this.cities, required this.onSelect, this.error});

  final List<OnboardingCity> cities;
  final ValueChanged<String> onSelect;
  final String? error;

  @override
  Widget build(BuildContext context) {
    return OnboardingStepScaffold(
      title: 'Choose your city',
      subtitle: 'Pick where your first company will be based.',
      error: error,
      child: Column(
        children: [
          for (final city in cities)
            Card(
              child: ListTile(
                key: ValueKey('city-${city.id}'),
                title: Text(city.name),
                subtitle: Text('${city.countryCode} · ${city.currencyCode} · pop. ${city.population}'),
                trailing: const FaIcon(AppIcons.chevronRight, size: 16),
                onTap: () => onSelect(city.id),
              ),
            ),
        ],
      ),
    );
  }
}

class OnboardingIndustryStep extends StatelessWidget {
  const OnboardingIndustryStep({
    super.key,
    required this.industries,
    required this.proOnlyIndustries,
    required this.onSelect,
    this.error,
  });

  final List<String> industries;
  final List<String> proOnlyIndustries;
  final ValueChanged<String> onSelect;
  final String? error;

  @override
  Widget build(BuildContext context) {
    return OnboardingStepScaffold(
      title: 'Choose your industry',
      subtitle: 'This determines your starter product line.',
      error: error,
      child: Column(
        children: [
          for (final industry in industries)
            Card(
              child: ListTile(
                key: ValueKey('industry-$industry'),
                title: Text(industry),
                trailing: proOnlyIndustries.contains(industry)
                    ? const Chip(label: Text('PRO'))
                    : const FaIcon(AppIcons.chevronRight, size: 16),
                onTap: () => onSelect(industry),
              ),
            ),
        ],
      ),
    );
  }
}

class OnboardingProductStep extends StatelessWidget {
  const OnboardingProductStep({super.key, required this.products, required this.onSelect, this.error});

  final List<OnboardingProductType> products;
  final ValueChanged<String> onSelect;
  final String? error;

  @override
  Widget build(BuildContext context) {
    return OnboardingStepScaffold(
      title: 'Choose your starter product',
      subtitle: 'You can add more products later.',
      error: error,
      child: Column(
        children: [
          for (final product in products)
            Card(
              child: ListTile(
                key: ValueKey('product-${product.id}'),
                title: Text(product.name),
                subtitle: Text(
                  '\$${product.basePrice.toStringAsFixed(2)} · ${product.baseCraftTicks} ticks to craft'
                  '${product.recipes.isNotEmpty ? ' · needs ${product.recipes.map((r) => r.ingredientName).join(', ')}' : ''}',
                ),
                trailing: const FaIcon(AppIcons.chevronRight, size: 16),
                onTap: () => onSelect(product.id),
              ),
            ),
        ],
      ),
    );
  }
}

class OnboardingIpoStep extends StatelessWidget {
  const OnboardingIpoStep({super.key, required this.onSelect, this.error});

  final ValueChanged<double> onSelect;
  final String? error;

  @override
  Widget build(BuildContext context) {
    return OnboardingStepScaffold(
      title: 'Choose your IPO plan',
      subtitle:
          'Founder contribution is a fixed \$${onboardingFounderContribution.toStringAsFixed(0)}. '
          'The raise target is additional public capital.',
      error: error,
      child: Column(
        children: [
          for (final plan in onboardingIpoPlans)
            Card(
              child: ListTile(
                key: ValueKey('ipo-${plan.raiseTarget}'),
                title: Text(plan.label),
                subtitle: Text(
                  'Raise \$${plan.raiseTarget.toStringAsFixed(0)} · '
                  'you keep ${(plan.founderOwnershipRatio * 100).toStringAsFixed(0)}% ownership',
                ),
                trailing: const FaIcon(AppIcons.chevronRight, size: 16),
                onTap: () => onSelect(plan.raiseTarget),
              ),
            ),
        ],
      ),
    );
  }
}

class OnboardingLotStep extends StatelessWidget {
  const OnboardingLotStep({
    super.key,
    required this.title,
    required this.subtitle,
    required this.lots,
    required this.buildingType,
    required this.availableCash,
    required this.selectedLotId,
    required this.onSelect,
    required this.onPurchase,
    required this.purchaseLabel,
    required this.submitting,
    this.referenceLot,
    this.error,
  });

  final String title;
  final String subtitle;
  final List<CityLot> lots;
  final String buildingType;
  final double availableCash;
  final String? selectedLotId;
  final ValueChanged<String> onSelect;
  final VoidCallback onPurchase;
  final String purchaseLabel;
  final bool submitting;
  final CityLot? referenceLot;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final suitable = lots.where((l) => l.suitableFor(buildingType)).toList();
    final recommended = recommendedLotIds(lots, buildingType);

    return OnboardingStepScaffold(
      title: title,
      subtitle: subtitle,
      error: error,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Available cash: \$${availableCash.toStringAsFixed(0)}', style: theme.textTheme.titleMedium),
          const SizedBox(height: 12),
          if (suitable.isEmpty) const Text('No suitable lots are available in this city right now.'),
          for (final lot in suitable)
            Card(
              color: selectedLotId == lot.id ? theme.colorScheme.primaryContainer : null,
              child: ListTile(
                key: ValueKey('lot-${lot.id}'),
                enabled: !lot.isOwned,
                title: Text(lot.name),
                subtitle: Text(
                  [
                    lot.district,
                    '\$${lot.price.toStringAsFixed(0)}',
                    if (lot.isOwned) 'Already owned',
                    if (referenceLot != null)
                      '${approxDistanceKm(lot.latitude, lot.longitude, referenceLot!.latitude, referenceLot!.longitude).toStringAsFixed(1)} km from factory',
                  ].join(' · '),
                ),
                trailing: recommended.contains(lot.id) ? const Chip(label: Text('Recommended')) : null,
                onTap: lot.isOwned ? null : () => onSelect(lot.id),
              ),
            ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: (selectedLotId == null || submitting) ? null : onPurchase,
            child: submitting
                ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                : Text(purchaseLabel),
          ),
        ],
      ),
    );
  }
}
