import 'package:capitalism_app/features/buildings/building_grid_models.dart';
import 'package:capitalism_app/features/buildings/building_link_helpers.dart';
import 'package:flutter_test/flutter_test.dart';

EditableGridUnit _unit(String type, int x, int y) => EditableGridUnit(id: '$type-$x-$y', unitType: type, gridX: x, gridY: y);

void main() {
  group('horizontal link state + cycle', () {
    test('none by default, forward on first click when left is a supply origin', () {
      final purchase = _unit('PURCHASE', 0, 0);
      final storage = _unit('STORAGE', 1, 0);
      final units = [purchase, storage];

      expect(getHorizontalLinkState(units, 0, 0), LinkState.none);

      applyHorizontalLinkCycle(purchase, storage, getHorizontalLinkState(units, 0, 0));
      expect(purchase.linkRight, isTrue);
      expect(storage.linkLeft, isFalse);
      expect(getHorizontalLinkState(units, 0, 0), LinkState.forward);

      applyHorizontalLinkCycle(purchase, storage, getHorizontalLinkState(units, 0, 0));
      expect(purchase.linkRight, isFalse);
      expect(storage.linkLeft, isTrue);
      expect(getHorizontalLinkState(units, 0, 0), LinkState.backward);

      applyHorizontalLinkCycle(purchase, storage, getHorizontalLinkState(units, 0, 0));
      expect(purchase.linkRight, isFalse);
      expect(storage.linkLeft, isFalse);
      expect(getHorizontalLinkState(units, 0, 0), LinkState.none);
    });

    test('sink on the right defaults to forward (flow flows into it)', () {
      final storage = _unit('STORAGE', 0, 0);
      final sales = _unit('PUBLIC_SALES', 1, 0);
      applyHorizontalLinkCycle(storage, sales, getHorizontalLinkState([storage, sales], 0, 0));
      expect(storage.linkRight, isTrue);
      expect(sales.linkLeft, isFalse);
    });

    test('a legacy both state collapses straight to none on next click', () {
      final left = _unit('STORAGE', 0, 0)..linkRight = true;
      final right = _unit('STORAGE', 1, 0)..linkLeft = true;
      final units = [left, right];
      expect(getHorizontalLinkState(units, 0, 0), LinkState.both);

      applyHorizontalLinkCycle(left, right, LinkState.both);
      expect(left.linkRight, isFalse);
      expect(right.linkLeft, isFalse);
    });
  });

  group('vertical link state', () {
    test('top/bottom pair via linkDown/linkUp', () {
      final top = _unit('PURCHASE', 0, 0);
      final bottom = _unit('STORAGE', 0, 1);
      final units = [top, bottom];
      applyVerticalLinkCycle(top, bottom, getVerticalLinkState(units, 0, 0));
      expect(top.linkDown, isTrue);
      expect(getVerticalLinkState(units, 0, 0), LinkState.forward);
    });
  });

  group('diagonal link states', () {
    test('primary diagonal connects (x,y) and (x+1,y+1) via linkDownRight/linkUpLeft', () {
      final topLeft = _unit('PURCHASE', 0, 0);
      final bottomRight = _unit('STORAGE', 1, 1);
      final units = [topLeft, bottomRight];
      expect(canTogglePrimaryDiagonalLink(units, 0, 0), isTrue);
      applyPrimaryDiagonalLinkCycle(topLeft, bottomRight, getPrimaryDiagonalLinkState(units, 0, 0));
      expect(topLeft.linkDownRight, isTrue);
      expect(getPrimaryDiagonalLinkState(units, 0, 0), LinkState.forward);
    });

    test('secondary diagonal connects (x+1,y) and (x,y+1) via linkDownLeft/linkUpRight', () {
      final topRight = _unit('PURCHASE', 1, 0);
      final bottomLeft = _unit('STORAGE', 0, 1);
      final units = [topRight, bottomLeft];
      expect(canToggleSecondaryDiagonalLink(units, 0, 0), isTrue);
      applySecondaryDiagonalLinkCycle(topRight, bottomLeft, getSecondaryDiagonalLinkState(units, 0, 0));
      expect(topRight.linkDownLeft, isTrue);
      expect(getSecondaryDiagonalLinkState(units, 0, 0), LinkState.forward);
    });
  });

  group('canToggle* guards', () {
    test('false unless both endpoint cells are occupied', () {
      final units = [_unit('PURCHASE', 0, 0)];
      expect(canToggleHorizontalLink(units, 0, 0), isFalse);
      expect(canToggleVerticalLink(units, 0, 0), isFalse);
      expect(canTogglePrimaryDiagonalLink(units, 0, 0), isFalse);
      expect(canToggleSecondaryDiagonalLink(units, 0, 0), isFalse);
    });
  });

  group('clearConnectionsAround', () {
    test('clears all 8 neighbors\' flags pointing into the removed cell', () {
      final center = _unit('STORAGE', 1, 1);
      final left = _unit('A', 0, 1)..linkRight = true;
      final right = _unit('B', 2, 1)..linkLeft = true;
      final up = _unit('C', 1, 0)..linkDown = true;
      final down = _unit('D', 1, 2)..linkUp = true;
      final upLeft = _unit('E', 0, 0)..linkDownRight = true;
      final upRight = _unit('F', 2, 0)..linkDownLeft = true;
      final downLeft = _unit('G', 0, 2)..linkUpRight = true;
      final downRight = _unit('H', 2, 2)..linkUpLeft = true;
      final units = [center, left, right, up, down, upLeft, upRight, downLeft, downRight];

      clearConnectionsAround(units, 1, 1);

      expect(left.linkRight, isFalse);
      expect(right.linkLeft, isFalse);
      expect(up.linkDown, isFalse);
      expect(down.linkUp, isFalse);
      expect(upLeft.linkDownRight, isFalse);
      expect(upRight.linkDownLeft, isFalse);
      expect(downLeft.linkUpRight, isFalse);
      expect(downRight.linkUpLeft, isFalse);
    });
  });
}
