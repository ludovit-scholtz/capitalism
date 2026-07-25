import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_grid_draft_controller.dart';
import 'package:capitalism_app/features/buildings/building_grid_models.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_building_detail_service.dart';

const _emptyBuilding = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
  name: 'Factory',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 0,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
  cityFxRate: 1,
);

const _factoryUnit = BuildingUnitDetail(id: 'unit-1', unitType: 'STORAGE', level: 1, resourceTypeId: null, productTypeId: null, minPrice: null, gridX: 0, gridY: 0);

const _cityFactoryBuilding = BuildingDetail(
  id: 'building-2',
  companyId: 'company-1',
  name: 'City Factory',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 0,
  isForSale: false,
  units: [_factoryUnit],
  pendingConfiguration: null,
  cityFxRate: 25.2,
);

BuildingGridDraftController _controllerFor(BuildingDetail building, {double? companyCash}) {
  final controller = BuildingGridDraftController();
  controller.loadFrom(
    building,
    catalog: const BuildingCatalog(resourceNames: {}, productNames: {}, resourceBasePrices: {}, productBasePrices: {}),
    companyCash: companyCash,
  );
  return controller;
}

void main() {
  group('allowedUnitsMap', () {
    test('matches the server-side per-building-type allow list', () {
      expect(allowedUnitTypesFor('MINE'), ['MINING', 'STORAGE', 'B2B_SALES']);
      expect(allowedUnitTypesFor('APARTMENT'), isEmpty);
    });
  });

  group('editing lifecycle', () {
    test('startEditing clones active units into draft and baseline', () {
      final controller = _controllerFor(_cityFactoryBuilding);
      controller.startEditing();
      expect(controller.isEditing, isTrue);
      expect(controller.draftUnits, hasLength(1));
      expect(controller.draftUnits.first.id, 'unit-1');
      // Must be a clone, not the same reference, so mutating the draft
      // doesn't corrupt activeUnits.
      controller.draftUnits.first.minPrice = 99;
      expect(controller.activeUnits.first.minPrice, isNull);
    });

    test('cancelEditing discards draft changes and exits edit mode', () {
      final controller = _controllerFor(_cityFactoryBuilding);
      controller.startEditing();
      controller.removeDraftUnit(0, 0);
      expect(controller.draftUnits, isEmpty);

      controller.cancelEditing();
      expect(controller.isEditing, isFalse);
      expect(controller.draftUnits, hasLength(1));
    });
  });

  group('placeUnit / removeDraftUnit', () {
    test('placing a unit on an empty cell adds it to the draft', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.startEditing();
      controller.clickDraftCell(0, 0);
      expect(controller.showUnitPicker, isTrue);

      controller.placeUnit('PURCHASE');
      expect(controller.draftUnits, hasLength(1));
      expect(controller.draftUnits.first.unitType, 'PURCHASE');
      expect(controller.showUnitPicker, isFalse);
    });

    test('placing replaces whatever was already at that draft position', () {
      final controller = _controllerFor(_cityFactoryBuilding);
      controller.startEditing();
      controller.clickDraftCell(0, 0); // occupied by STORAGE -> picker stays closed
      expect(controller.showUnitPicker, isFalse);

      controller.selectedCell = (x: 0, y: 0);
      controller.placeUnit('MINING');
      expect(controller.draftUnits, hasLength(1));
      expect(controller.draftUnits.first.unitType, 'MINING');
    });

    test('a new B2B_SALES unit defaults saleVisibility to GROUP and suggests a price from an adjacent MANUFACTURING unit', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.catalog = const BuildingCatalog(
        resourceNames: {},
        productNames: {'steel': 'Steel'},
        resourceBasePrices: {},
        productBasePrices: {'steel': 100},
      );
      controller.startEditing();
      controller.draftUnits.add(EditableGridUnit(id: 'mfg', unitType: 'MANUFACTURING', gridX: 0, gridY: 0, productTypeId: 'steel'));
      controller.clickDraftCell(1, 0);
      controller.placeUnit('B2B_SALES');

      final placed = controller.draftUnitAt(1, 0)!;
      expect(placed.saleVisibility, 'GROUP');
      expect(placed.minPrice, 100); // cityFxRate 1 for _emptyBuilding
    });

    test('removeDraftUnit clears the cell and neighbor link flags pointing into it', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.startEditing();
      controller.draftUnits.addAll([
        EditableGridUnit(id: 'a', unitType: 'PURCHASE', gridX: 0, gridY: 0, linkRight: true),
        EditableGridUnit(id: 'b', unitType: 'STORAGE', gridX: 1, gridY: 0, linkLeft: true),
      ]);

      controller.removeDraftUnit(1, 0);

      expect(controller.draftUnitAt(1, 0), isNull);
      expect(controller.draftUnitAt(0, 0)!.linkRight, isFalse);
    });

    test('removing a unit also unstages any staged upgrade for it', () {
      final controller = _controllerFor(_cityFactoryBuilding);
      controller.startEditing();
      controller.toggleStagedUpgrade('unit-1');
      expect(controller.draftUpgradeUnitIds, contains('unit-1'));

      controller.removeDraftUnit(0, 0);
      expect(controller.draftUpgradeUnitIds, isEmpty);
    });
  });

  group('link toggles', () {
    test('toggleHorizontalLink cycles through the draft grid only', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.startEditing();
      controller.draftUnits.addAll([
        EditableGridUnit(id: 'a', unitType: 'PURCHASE', gridX: 0, gridY: 0),
        EditableGridUnit(id: 'b', unitType: 'STORAGE', gridX: 1, gridY: 0),
      ]);

      controller.toggleHorizontalLink(0, 0);
      expect(controller.draftUnitAt(0, 0)!.linkRight, isTrue);
    });

    test('toggling does nothing outside edit mode', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.draftUnits.addAll([
        EditableGridUnit(id: 'a', unitType: 'PURCHASE', gridX: 0, gridY: 0),
        EditableGridUnit(id: 'b', unitType: 'STORAGE', gridX: 1, gridY: 0),
      ]);
      controller.toggleHorizontalLink(0, 0);
      expect(controller.draftUnitAt(0, 0)!.linkRight, isFalse);
    });
  });

  group('starter layouts', () {
    test('applyStarterLayout populates the FACTORY chain and forces hasDraftChanges true', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.applyStarterLayout();

      expect(controller.isEditing, isTrue);
      expect(controller.draftUnits.map((u) => u.unitType).toList(), ['PURCHASE', 'MANUFACTURING', 'STORAGE', 'B2B_SALES']);
      expect(controller.hasDraftChanges, isTrue);
    });

    test('applyShopStarterLayout populates the 2-unit shop chain', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.applyShopStarterLayout();
      expect(controller.draftUnits.map((u) => u.unitType).toList(), ['PURCHASE', 'PUBLIC_SALES']);
    });
  });

  group('draft summary', () {
    test('draftUnitChanges reports an added unit with construction cost and ticks', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.startEditing();
      controller.clickDraftCell(0, 0);
      controller.placeUnit('MINING');

      final changes = controller.draftUnitChanges;
      expect(changes, hasLength(1));
      expect(changes.first.changeType, 'added');
      expect(changes.first.cost, 9000); // MINING base cost, cityFxRate 1
      expect(changes.first.ticks, 3);
    });

    test('draftConstructionCost is FX-converted to the city currency', () {
      final controller = _controllerFor(_cityFactoryBuilding); // cityFxRate 25.2
      controller.startEditing();
      controller.clickDraftCell(1, 1);
      controller.placeUnit('MINING'); // 9000 EUR base
      expect(controller.draftConstructionCost, closeTo(9000 * 25.2, 0.01));
    });

    test('projectedCompanyCashAfterApply subtracts the construction cost from company cash', () {
      final controller = _controllerFor(_emptyBuilding, companyCash: 50000);
      controller.startEditing();
      controller.clickDraftCell(0, 0);
      controller.placeUnit('MINING'); // 9000
      expect(controller.projectedCompanyCashAfterApply, 41000);
    });

    test('draftLinkChanges reports one entry per flipped directional flag', () {
      final controller = _controllerFor(_emptyBuilding);
      controller.startEditing();
      controller.draftUnits.addAll([
        EditableGridUnit(id: 'a', unitType: 'PURCHASE', gridX: 0, gridY: 0),
        EditableGridUnit(id: 'b', unitType: 'STORAGE', gridX: 1, gridY: 0),
      ]);
      controller.toggleHorizontalLink(0, 0);

      final changes = controller.draftLinkChanges;
      expect(changes, hasLength(1));
      expect(changes.first.added, isTrue);
    });
  });

  group('storeConfiguration / cancelPlan', () {
    test('storeConfiguration sends the full draft unit list and exits edit mode on success', () async {
      final controller = _controllerFor(_emptyBuilding);
      final service = FakeBuildingDetailService();
      controller.startEditing();
      controller.clickDraftCell(0, 0);
      controller.placeUnit('PURCHASE');

      final ok = await controller.storeConfiguration(service);

      expect(ok, isTrue);
      expect(controller.isEditing, isFalse);
      expect(service.lastStoredBuildingId, 'building-1');
      expect(service.lastStoredUnits, hasLength(1));
      expect(service.lastStoredUnits!.first.unitType, 'PURCHASE');
    });

    test('storeConfiguration is a no-op when there are no draft changes', () async {
      final controller = _controllerFor(_cityFactoryBuilding);
      final service = FakeBuildingDetailService();
      controller.startEditing(); // draft == baseline, no changes

      final ok = await controller.storeConfiguration(service);

      expect(ok, isFalse);
      expect(service.calls, isEmpty);
    });

    test('storeConfiguration surfaces a server error via saveError', () async {
      final controller = _controllerFor(_emptyBuilding);
      final service = FakeBuildingDetailService(storeConfigurationError: Exception('boom'));
      controller.startEditing();
      controller.clickDraftCell(0, 0);
      controller.placeUnit('PURCHASE');

      final ok = await controller.storeConfiguration(service);

      expect(ok, isFalse);
      expect(controller.saveError, isNotNull);
      expect(controller.isEditing, isTrue);
    });

    test('cancelPlan calls cancelBuildingConfiguration only when a plan is pending', () async {
      const withPending = BuildingDetail(
        id: 'b',
        companyId: 'c',
        name: 'n',
        type: 'FACTORY',
        level: 1,
        powerStatus: null,
        occupancyPercent: null,
        isForSale: false,
        units: [],
        pendingConfiguration: PendingBuildingConfiguration(appliesAtTick: 5, totalTicksRequired: 3, blockReason: null),
      );
      final controller = _controllerFor(withPending);
      final service = FakeBuildingDetailService();

      final ok = await controller.cancelPlan(service);
      expect(ok, isTrue);
      expect(service.lastCancelledBuildingId, 'b');
    });

    test('cancelPlan is a no-op without a pending configuration', () async {
      final controller = _controllerFor(_emptyBuilding);
      final service = FakeBuildingDetailService();
      final ok = await controller.cancelPlan(service);
      expect(ok, isFalse);
      expect(service.calls, isEmpty);
    });
  });
}
