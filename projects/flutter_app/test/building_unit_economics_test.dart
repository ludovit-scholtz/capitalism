import 'package:capitalism_app/features/buildings/building_grid_models.dart';
import 'package:capitalism_app/features/buildings/building_unit_economics.dart';
import 'package:flutter_test/flutter_test.dart';

EditableGridUnit _unit(String type, int x, int y) => EditableGridUnit(id: '$type-$x-$y', unitType: type, gridX: x, gridY: y);

void main() {
  group('getUnitConstructionCost', () {
    test('returns the EUR base cost for a known type and 0 for unknown', () {
      expect(getUnitConstructionCost('MINING'), 9000);
      expect(getUnitConstructionCost('MANUFACTURING'), 12000);
      expect(getUnitConstructionCost('NOT_A_TYPE'), 0);
    });
  });

  group('getPlannedUnitConstructionCost', () {
    test('full cost for a brand-new placement (no active unit)', () {
      expect(getPlannedUnitConstructionCost(null, _unit('MINING', 0, 0)), 9000);
    });

    test('full cost for a type replacement at the same position', () {
      final active = _unit('STORAGE', 0, 0);
      final planned = _unit('MINING', 0, 0);
      expect(getPlannedUnitConstructionCost(active, planned), 9000);
    });

    test('zero cost when the type is unchanged (pure reconfiguration)', () {
      final active = _unit('MINING', 0, 0);
      final planned = _unit('MINING', 0, 0);
      expect(getPlannedUnitConstructionCost(active, planned), 0);
    });
  });

  group('sumPlannedConfigurationCost', () {
    test('sums cost across every touched position', () {
      final active = [_unit('STORAGE', 0, 0)];
      final planned = [_unit('MINING', 0, 0), _unit('STORAGE', 1, 0)];
      // (0,0): STORAGE->MINING = 9000 new type. (1,0): no active unit -> STORAGE = 3500.
      expect(sumPlannedConfigurationCost(active, planned), 9000 + 3500);
    });
  });

  group('calculateCancelTicks', () {
    test('10% of original, rounded up, minimum 1', () {
      expect(calculateCancelTicks(3), 1);
      expect(calculateCancelTicks(10), 1);
      expect(calculateCancelTicks(11), 2);
      expect(calculateCancelTicks(100), 10);
    });
  });

  group('calculateTicksRequired', () {
    test('3 ticks for a brand-new unit', () {
      expect(calculateTicksRequired(null, _unit('MINING', 0, 0)), unitPlanChangeTicks);
    });

    test('3 ticks for a type change at the same position', () {
      expect(calculateTicksRequired(_unit('STORAGE', 0, 0), _unit('MINING', 0, 0)), unitPlanChangeTicks);
    });

    test('1 tick for a link-flag-only change', () {
      final active = _unit('STORAGE', 0, 0);
      final desired = _unit('STORAGE', 0, 0)..linkRight = true;
      expect(calculateTicksRequired(active, desired), linkChangeTicks);
    });

    test('1 tick for a config-field-only change', () {
      final active = _unit('PURCHASE', 0, 0);
      final desired = _unit('PURCHASE', 0, 0)..resourceTypeId = 'iron-ore';
      expect(calculateTicksRequired(active, desired), linkChangeTicks);
    });

    test('0 ticks when nothing actually changed', () {
      final active = _unit('STORAGE', 0, 0);
      final desired = _unit('STORAGE', 0, 0);
      expect(calculateTicksRequired(active, desired), 0);
    });
  });
}
