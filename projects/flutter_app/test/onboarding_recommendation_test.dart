import 'package:capitalism_app/features/onboarding/onboarding_models.dart';
import 'package:capitalism_app/features/onboarding/onboarding_recommendation.dart';
import 'package:flutter_test/flutter_test.dart';

CityLot _lot({
  required String id,
  String district = '',
  double latitude = 0,
  double longitude = 0,
  double populationIndex = 0,
  double price = 0,
  List<String> suitableTypes = const ['FACTORY'],
  String? ownerCompanyId,
}) => CityLot(
  id: id,
  cityId: 'city-1',
  name: id,
  district: district,
  latitude: latitude,
  longitude: longitude,
  populationIndex: populationIndex,
  price: price,
  suitableTypes: suitableTypes,
  ownerCompanyId: ownerCompanyId,
);

void main() {
  group('availableLotsFor', () {
    test('excludes owned lots and lots not suitable for the building type', () {
      final lots = [
        _lot(id: 'a', suitableTypes: const ['FACTORY']),
        _lot(id: 'b', suitableTypes: const ['FACTORY'], ownerCompanyId: 'company-1'),
        _lot(id: 'c', suitableTypes: const ['SALES_SHOP']),
      ];
      expect(availableLotsFor(lots, 'FACTORY').map((l) => l.id), ['a']);
    });
  });

  group('recommendedFactoryLotIds', () {
    test('prefers industrial-district lots sorted by ascending price', () {
      final lots = [
        _lot(id: 'expensive-industrial', district: 'Industrial Zone', price: 9000),
        _lot(id: 'cheap-industrial', district: 'industrial', price: 1000),
        _lot(id: 'cheap-other', district: 'Suburb', price: 500),
      ];
      expect(recommendedFactoryLotIds(lots), ['cheap-industrial', 'expensive-industrial']);
    });

    test('falls back to all unowned lots sorted by price when no industrial-district lot exists', () {
      final lots = [
        _lot(id: 'b', district: 'Suburb', price: 2000),
        _lot(id: 'a', district: 'Downtown', price: 1000),
        _lot(id: 'c', district: 'Uptown', price: 3000),
      ];
      expect(recommendedFactoryLotIds(lots, count: 2), ['a', 'b']);
    });

    test('respects the count parameter', () {
      final lots = [
        _lot(id: 'a', district: 'Industrial', price: 100),
        _lot(id: 'b', district: 'Industrial', price: 200),
        _lot(id: 'c', district: 'Industrial', price: 300),
      ];
      expect(recommendedFactoryLotIds(lots, count: 1), ['a']);
    });
  });

  group('recommendedShopLotIds', () {
    test('prefers commercial/business-district lots by descending population index', () {
      final lots = [
        _lot(id: 'low-pop', district: 'Commercial', populationIndex: 0.2, price: 1000),
        _lot(id: 'high-pop', district: 'business district', populationIndex: 0.9, price: 5000),
        _lot(id: 'not-commercial', district: 'Industrial', populationIndex: 1, price: 1),
      ];
      expect(recommendedShopLotIds(lots), ['high-pop', 'low-pop']);
    });

    test('treats population differences under the 0.15 threshold as a tie and falls back to distance-to-factory', () {
      final factoryLot = _lot(id: 'factory', latitude: 0, longitude: 0);
      final near = _lot(id: 'near', district: 'Commercial', populationIndex: 0.50, latitude: 0.001, longitude: 0.001);
      final far = _lot(id: 'far', district: 'Commercial', populationIndex: 0.55, latitude: 5, longitude: 5);
      expect(recommendedShopLotIds([near, far], factoryLot: factoryLot), ['near', 'far']);
    });

    test('falls back to price when population and distance are both tied', () {
      final cheap = _lot(id: 'cheap', district: 'Commercial', populationIndex: 0.5, price: 100);
      final expensive = _lot(id: 'expensive', district: 'Commercial', populationIndex: 0.5, price: 500);
      expect(recommendedShopLotIds([expensive, cheap]), ['cheap', 'expensive']);
    });

    test('falls back to all unowned lots when no commercial/business-district lot exists', () {
      final lots = [
        _lot(id: 'a', district: 'Suburb', populationIndex: 0.5, price: 100, suitableTypes: const ['SALES_SHOP']),
        _lot(id: 'b', district: 'Suburb', populationIndex: 0.9, price: 100, suitableTypes: const ['SALES_SHOP']),
      ];
      expect(recommendedShopLotIds(lots), ['b', 'a']);
    });
  });

  group('defaultRecommendedLotId', () {
    test('picks the first recommended lot that is affordable', () {
      final lots = [_lot(id: 'a', price: 1000), _lot(id: 'b', price: 100)];
      expect(defaultRecommendedLotId(lots, ['a', 'b'], 500), 'b');
    });

    test('falls back to the first affordable lot when no recommended lot is affordable', () {
      final lots = [_lot(id: 'a', price: 1000), _lot(id: 'b', price: 100)];
      expect(defaultRecommendedLotId(lots, ['a'], 500), 'b');
    });

    test('returns empty string when nothing is affordable', () {
      final lots = [_lot(id: 'a', price: 1000)];
      expect(defaultRecommendedLotId(lots, ['a'], 500), '');
    });

    test('excludes owned lots even if listed as recommended', () {
      final lots = [_lot(id: 'a', price: 100, ownerCompanyId: 'company-1'), _lot(id: 'b', price: 100)];
      expect(defaultRecommendedLotId(lots, ['a'], 500), 'b');
    });
  });
}
