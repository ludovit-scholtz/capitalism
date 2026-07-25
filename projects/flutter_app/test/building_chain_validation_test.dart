import 'package:capitalism_app/features/buildings/building_chain_validation.dart';
import 'package:capitalism_app/features/buildings/building_grid_models.dart';
import 'package:flutter_test/flutter_test.dart';

EditableGridUnit _unit(String type, int x, int y) => EditableGridUnit(id: '$type-$x-$y', unitType: type, gridX: x, gridY: y);

void main() {
  group('getConfigWarnings', () {
    test('flags an unconfigured purchase unit', () {
      final warnings = getConfigWarnings('FACTORY', [_unit('PURCHASE', 0, 0)]);
      expect(warnings.any((w) => w.message.contains('no resource or product selected')), isTrue);
    });

    test('flags a manufacturing unit not linked to a storage/sales output, even through a passthrough chain', () {
      final purchase = _unit('PURCHASE', 0, 0)
        ..resourceTypeId = 'iron-ore'
        ..linkRight = true;
      final mfg1 = _unit('MANUFACTURING', 1, 0)
        ..productTypeId = 'steel'
        ..linkLeft = true;
      // mfg1 not linked onward -> unreachable.
      final warnings = getConfigWarnings('FACTORY', [purchase, mfg1]);
      expect(warnings.any((w) => w.message.contains('Manufacturing unit at (1, 0) is not linked')), isTrue);
    });

    test('manufacturing reaches storage through a passthrough manufacturing unit', () {
      final mfg1 = _unit('MANUFACTURING', 0, 0)
        ..productTypeId = 'steel'
        ..linkRight = true;
      final mfg2 = _unit('MANUFACTURING', 1, 0)
        ..productTypeId = 'steel'
        ..linkLeft = true
        ..linkRight = true;
      final storage = _unit('STORAGE', 2, 0)..linkLeft = true;
      final warnings = getConfigWarnings('FACTORY', [mfg1, mfg2, storage]);
      expect(warnings.any((w) => w.message.contains('Manufacturing unit at (0, 0) is not linked')), isFalse);
    });

    test('mining requires a direct one-hop link (no passthrough)', () {
      final mining = _unit('MINING', 0, 0)
        ..resourceTypeId = 'iron-ore'
        ..linkRight = true;
      final storage = _unit('STORAGE', 1, 0)
        ..linkLeft = true
        ..linkRight = true;
      final farStorage = _unit('STORAGE', 2, 0)..linkLeft = true;
      // Mining -> Storage(1,0) -> Storage(2,0): mining links directly to a
      // STORAGE neighbor, which is itself a valid terminal, so this is fine.
      final warnings = getConfigWarnings('MINE', [mining, storage, farStorage]);
      expect(warnings.any((w) => w.message.contains('Mining unit')), isFalse);
    });

    test('storage with no links at all is flagged', () {
      final warnings = getConfigWarnings('FACTORY', [_unit('STORAGE', 0, 0)]);
      expect(warnings.any((w) => w.message.contains('Storage unit at (0, 0) is not linked to any other unit')), isTrue);
    });
  });

  group('getProductionChainStatus', () {
    test('complete once purchase has an item and manufacturing has a product, ignoring links', () {
      final purchase = _unit('PURCHASE', 0, 0)..resourceTypeId = 'iron-ore';
      final mfg = _unit('MANUFACTURING', 3, 3)..productTypeId = 'steel'; // deliberately unlinked/far away
      final status = getProductionChainStatus([purchase, mfg]);
      expect(status.isChainComplete, isTrue);
    });

    test('incomplete when manufacturing has no product', () {
      final purchase = _unit('PURCHASE', 0, 0)..resourceTypeId = 'iron-ore';
      final mfg = _unit('MANUFACTURING', 1, 0);
      final status = getProductionChainStatus([purchase, mfg]);
      expect(status.isChainComplete, isFalse);
      expect(status.isPurchaseConfigured, isTrue);
      expect(status.isManufacturingConfigured, isFalse);
    });
  });

  group('getShopChainStatus', () {
    test('complete once purchase has an item and public sales has a product + price', () {
      final purchase = _unit('PURCHASE', 0, 0)..productTypeId = 'steel';
      final sales = _unit('PUBLIC_SALES', 1, 0)
        ..productTypeId = 'steel'
        ..minPrice = 10;
      final status = getShopChainStatus([purchase, sales]);
      expect(status.isChainComplete, isTrue);
    });

    test('incomplete when public sales has no price', () {
      final purchase = _unit('PURCHASE', 0, 0)..productTypeId = 'steel';
      final sales = _unit('PUBLIC_SALES', 1, 0)..productTypeId = 'steel';
      final status = getShopChainStatus([purchase, sales]);
      expect(status.isChainComplete, isFalse);
    });
  });
}
