import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart' show TileProvider;
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:latlong2/latlong.dart';

import '../../core/theme/app_icons.dart';
import '../../core/widgets/capitalism_map_view.dart';
import 'onboarding_models.dart';
import 'onboarding_recommendation.dart';

CityLot? _findLot(List<CityLot> lots, String? id) {
  if (id == null) return null;
  for (final lot in lots) {
    if (lot.id == id) return lot;
  }
  return null;
}

/// Web's `OnboardingLotSelector` uses this same simplified approximation
/// (not true haversine) for both city-center and factory-to-shop distance.
double approxDistanceKm(double lat1, double lon1, double lat2, double lon2) {
  final dLat = lat1 - lat2;
  final dLon = lon1 - lon2;
  return math.sqrt(dLat * dLat + dLon * dLon) * 111;
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
  const OnboardingProductStep({
    super.key,
    required this.products,
    required this.onSelect,
    required this.formatBasePrice,
    this.error,
  });

  final List<OnboardingProductType> products;
  final ValueChanged<String> onSelect;

  /// Converts a product's USD-nominal `basePrice` into the selected city's
  /// currency and formats it (see `onboarding_fx.dart`).
  final String Function(double basePrice) formatBasePrice;
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
                  '${formatBasePrice(product.basePrice)} · ${product.baseCraftTicks} ticks to craft'
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
  const OnboardingIpoStep({super.key, required this.onSelect, required this.formatUsdWhole, this.error});

  final ValueChanged<double> onSelect;

  /// Converts a USD-nominal whole-unit amount (founder contribution, raise
  /// target) into the selected city's currency and formats it.
  final String Function(double usdAmount) formatUsdWhole;
  final String? error;

  @override
  Widget build(BuildContext context) {
    return OnboardingStepScaffold(
      title: 'Choose your IPO plan',
      subtitle:
          'Founder contribution is a fixed ${formatUsdWhole(onboardingFounderContribution)}. '
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
                  'Raise ${formatUsdWhole(plan.raiseTarget)} · '
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
    required this.formatAmount,
    this.referenceLot,
    this.tileProvider,
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

  /// Injectable so widget tests never hit real OSM tile servers — see
  /// `test/support/fake_tile_provider.dart`.
  final TileProvider? tileProvider;

  /// Formats an amount already denominated in the selected city's currency
  /// (available cash, lot prices — both already local-currency from the
  /// backend). No conversion, symbol/decimals only.
  final String Function(double amount) formatAmount;

  /// Marker color mirroring web's `OnboardingLotSelector.getMarkerColor`:
  /// selected (blue) > owned (gray) > recommended-and-affordable (green) >
  /// affordable-only (orange) > neither (gray).
  Color _markerColor(CityLot lot, List<String> recommended) {
    if (selectedLotId == lot.id) return CapitalismMapColors.selected;
    if (lot.isOwned) return CapitalismMapColors.ownedByNpc;
    final affordable = lot.price <= availableCash;
    if (recommended.contains(lot.id) && affordable) return CapitalismMapColors.available;
    if (affordable) return CapitalismMapColors.affordableOnly;
    return CapitalismMapColors.ownedByNpc;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final suitable = lots.where((l) => l.suitableFor(buildingType)).toList();
    final available = availableLotsFor(lots, buildingType);
    final recommended = buildingType == 'FACTORY'
        ? recommendedFactoryLotIds(available)
        : recommendedShopLotIds(available, factoryLot: referenceLot);
    final selectedLot = _findLot(suitable, selectedLotId);

    return OnboardingStepScaffold(
      title: title,
      subtitle: subtitle,
      error: error,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Available cash: ${formatAmount(availableCash)}', style: theme.textTheme.titleMedium),
          const SizedBox(height: 12),
          if (suitable.isEmpty)
            const Text('No suitable lots are available in this city right now.')
          else ...[
            ClipRRect(
              borderRadius: BorderRadius.circular(12),
              child: SizedBox(
                height: 260,
                child: CapitalismMapView(
                  tileProvider: tileProvider,
                  flyToTarget: selectedLot != null ? LatLng(selectedLot.latitude, selectedLot.longitude) : null,
                  markers: [
                    for (final lot in suitable)
                      CapitalismMapMarker(
                        id: lot.id,
                        position: LatLng(lot.latitude, lot.longitude),
                        color: _markerColor(lot, recommended),
                        size: selectedLotId == lot.id ? 20 : 14,
                        tooltip: lot.name,
                        onTap: lot.isOwned ? null : () => onSelect(lot.id),
                      ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 12),
          ],
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
                    formatAmount(lot.price),
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
