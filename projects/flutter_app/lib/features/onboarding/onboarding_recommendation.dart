// Ported from `projects/frontend/src/lib/onboardingHelpers.ts` — the real
// district-name-regex + population-index recommendation heuristics, replacing
// the previous "two cheapest unowned lots" stand-in.

import 'dart:math' as math;

import 'onboarding_models.dart';

const double _populationIndexPriorityThreshold = 0.15;
const double _sameLocationDistanceEpsilon = 0.00001;

final RegExp _industrialDistrictPattern = RegExp('industrial', caseSensitive: false);
final RegExp _commercialDistrictPattern = RegExp('(commercial|business)', caseSensitive: false);

int _sign(double value) => value < 0 ? -1 : (value > 0 ? 1 : 0);

/// Unowned lots suitable for [buildingType] — mirrors web's `getAvailableLots`.
List<CityLot> availableLotsFor(List<CityLot> lots, String buildingType) =>
    lots.where((lot) => !lot.isOwned && lot.suitableFor(buildingType)).toList();

/// Raw (non-haversine) Euclidean lat/lng distance, matching web's
/// `getLotDistanceScore` — used only for shop-lot tie-breaking against the
/// chosen factory lot, not for any on-screen distance display.
double _lotDistanceScore(CityLot a, CityLot b) {
  final dLat = a.latitude - b.latitude;
  final dLon = a.longitude - b.longitude;
  return math.sqrt(dLat * dLat + dLon * dLon);
}

int _compareShopLots(CityLot a, CityLot b, CityLot? factoryLot) {
  final populationScore = b.populationIndex - a.populationIndex;
  if (populationScore.abs() >= _populationIndexPriorityThreshold) {
    return _sign(populationScore);
  }
  if (factoryLot != null) {
    final distanceScore = _lotDistanceScore(a, factoryLot) - _lotDistanceScore(b, factoryLot);
    if (distanceScore.abs() > _sameLocationDistanceEpsilon) {
      return _sign(distanceScore);
    }
  }
  return _sign(a.price - b.price);
}

/// Recommended factory-lot IDs: unowned lots in an "industrial" district,
/// sorted by ascending price; falls back to all unowned lots by price if no
/// industrial-district lot exists. Mirrors `getRecommendedFactoryLotIds`.
List<String> recommendedFactoryLotIds(List<CityLot> availableFactoryLots, {int count = 2}) {
  final unowned = availableFactoryLots.where((lot) => !lot.isOwned).toList();
  final industrial = unowned.where((lot) => _industrialDistrictPattern.hasMatch(lot.district)).toList()
    ..sort((a, b) => a.price.compareTo(b.price));

  if (industrial.isNotEmpty) {
    return industrial.take(count).map((lot) => lot.id).toList();
  }

  final sorted = [...unowned]..sort((a, b) => a.price.compareTo(b.price));
  return sorted.take(count).map((lot) => lot.id).toList();
}

/// Recommended shop-lot IDs: unowned lots in a "commercial"/"business"
/// district, sorted by descending population index (tie-tolerant within
/// [_populationIndexPriorityThreshold]), then ascending raw distance to
/// [factoryLot] (tie-tolerant within [_sameLocationDistanceEpsilon]), then
/// ascending price. Falls back to all unowned lots with the same ordering if
/// no commercial/business-district lot exists. Mirrors
/// `getRecommendedShopLotIds`.
List<String> recommendedShopLotIds(List<CityLot> availableShopLots, {int count = 2, CityLot? factoryLot}) {
  final unowned = availableShopLots.where((lot) => !lot.isOwned).toList();
  final commercial = unowned.where((lot) => _commercialDistrictPattern.hasMatch(lot.district)).toList()
    ..sort((a, b) => _compareShopLots(a, b, factoryLot));

  if (commercial.isNotEmpty) {
    return commercial.take(count).map((lot) => lot.id).toList();
  }

  final sorted = [...unowned]..sort((a, b) => _compareShopLots(a, b, factoryLot));
  return sorted.take(count).map((lot) => lot.id).toList();
}

/// First recommended lot that's also affordable, else the first affordable
/// lot, else `''`. Mirrors `getDefaultRecommendedLotId`.
String defaultRecommendedLotId(List<CityLot> availableLots, List<String> recommendedLotIds, double moneyAvailable) {
  final validLots = availableLots.where((lot) => !lot.isOwned && lot.price <= moneyAvailable).toList();
  final validById = {for (final lot in validLots) lot.id: lot};

  for (final id in recommendedLotIds) {
    if (validById.containsKey(id)) return id;
  }
  return validLots.isNotEmpty ? validLots.first.id : '';
}
