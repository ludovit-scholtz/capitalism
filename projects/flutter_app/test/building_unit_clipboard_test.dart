import 'package:capitalism_app/features/buildings/building_grid_models.dart';
import 'package:capitalism_app/features/buildings/building_unit_clipboard.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('serializeUnitConfig / deserializeUnitConfig round trip', () {
    test('round-trips all config fields, excluding position/link/structural fields', () {
      final unit = EditableGridUnit(
        id: 'unit-1',
        unitType: 'PURCHASE',
        gridX: 2,
        gridY: 3,
        level: 4,
        linkRight: true,
        resourceTypeId: 'iron-ore',
        maxPrice: 12.5,
        purchaseSource: 'EXCHANGE',
        minQuality: 0.5,
      );

      final serialized = serializeUnitConfig(unit);
      final result = deserializeUnitConfig(serialized);

      expect(result.isOk, isTrue);
      expect(result.config!.unitType, 'PURCHASE');
      expect(result.config!.resourceTypeId, 'iron-ore');
      expect(result.config!.maxPrice, 12.5);
      expect(result.config!.purchaseSource, 'EXCHANGE');
      expect(result.config!.minQuality, 0.5);
    });

    test('empty clipboard returns an empty error', () {
      final result = deserializeUnitConfig(null);
      expect(result.isOk, isFalse);
      expect(result.error, ClipboardParseError.empty);

      final resultBlank = deserializeUnitConfig('   ');
      expect(resultBlank.error, ClipboardParseError.empty);
    });

    test('malformed JSON returns an invalidJson error', () {
      final result = deserializeUnitConfig('not json at all');
      expect(result.error, ClipboardParseError.invalidJson);
    });

    test('mismatched schema version returns a schemaMismatch error', () {
      final result = deserializeUnitConfig('{"__schema":"unit-config-v0","unitType":"PURCHASE"}');
      expect(result.error, ClipboardParseError.schemaMismatch);
    });

    test('pasting onto an occupied cell enforces a strict unit-type match', () {
      final unit = EditableGridUnit(id: 'u', unitType: 'MINING', gridX: 0, gridY: 0);
      final serialized = serializeUnitConfig(unit);

      final matching = deserializeUnitConfig(serialized, targetUnitType: 'MINING');
      expect(matching.isOk, isTrue);

      final mismatched = deserializeUnitConfig(serialized, targetUnitType: 'STORAGE');
      expect(mismatched.error, ClipboardParseError.incompatibleType);
    });

    test('pasting onto an empty cell (no targetUnitType) accepts any valid payload', () {
      final unit = EditableGridUnit(id: 'u', unitType: 'MINING', gridX: 0, gridY: 0);
      final result = deserializeUnitConfig(serializeUnitConfig(unit));
      expect(result.isOk, isTrue);
    });
  });

  group('applyConfigToUnit', () {
    test('overwrites config fields but never touches position/id/level/links', () {
      final unit = EditableGridUnit(id: 'unit-1', unitType: 'PURCHASE', gridX: 2, gridY: 3, level: 4, linkRight: true);
      const config = ClipboardUnitConfig(unitType: 'PURCHASE', resourceTypeId: 'iron-ore', maxPrice: 12.5, purchaseSource: 'LOCAL');

      applyConfigToUnit(unit, config);

      expect(unit.resourceTypeId, 'iron-ore');
      expect(unit.maxPrice, 12.5);
      expect(unit.purchaseSource, 'LOCAL');
      expect(unit.id, 'unit-1');
      expect(unit.gridX, 2);
      expect(unit.gridY, 3);
      expect(unit.level, 4);
      expect(unit.linkRight, isTrue);
    });
  });
}
