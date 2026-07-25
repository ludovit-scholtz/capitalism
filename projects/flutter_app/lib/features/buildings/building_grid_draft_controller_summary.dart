part of 'building_grid_draft_controller.dart';

class LinkChangeEntry {
  const LinkChangeEntry({required this.description, required this.added});
  final String description;
  final bool added;
}

class UnitChangeEntry {
  const UnitChangeEntry({
    required this.changeType,
    required this.gridX,
    required this.gridY,
    required this.unitType,
    this.previousUnitType,
    required this.ticks,
    required this.cost,
  });

  final String changeType; // 'added' | 'removed' | 'replaced'
  final int gridX;
  final int gridY;
  final String unitType;
  final String? previousUnitType;
  final int ticks;
  final double cost;
}

const List<({String flag, int dx, int dy, String label})> _linkDirections = [
  (flag: 'linkRight', dx: 1, dy: 0, label: 'right'),
  (flag: 'linkLeft', dx: -1, dy: 0, label: 'left'),
  (flag: 'linkDown', dx: 0, dy: 1, label: 'down'),
  (flag: 'linkUp', dx: 0, dy: -1, label: 'up'),
  (flag: 'linkDownRight', dx: 1, dy: 1, label: 'down-right'),
  (flag: 'linkDownLeft', dx: -1, dy: 1, label: 'down-left'),
  (flag: 'linkUpRight', dx: 1, dy: -1, label: 'up-right'),
  (flag: 'linkUpLeft', dx: -1, dy: -1, label: 'up-left'),
];

bool _flagValue(EditableGridUnit unit, String flag) {
  switch (flag) {
    case 'linkRight':
      return unit.linkRight;
    case 'linkLeft':
      return unit.linkLeft;
    case 'linkDown':
      return unit.linkDown;
    case 'linkUp':
      return unit.linkUp;
    case 'linkDownRight':
      return unit.linkDownRight;
    case 'linkDownLeft':
      return unit.linkDownLeft;
    case 'linkUpRight':
      return unit.linkUpRight;
    case 'linkUpLeft':
      return unit.linkUpLeft;
    default:
      return false;
  }
}

int _compareUnits(EditableGridUnit a, EditableGridUnit b) {
  if (a.gridY != b.gridY) return a.gridY - b.gridY;
  if (a.gridX != b.gridX) return a.gridX - b.gridX;
  return a.unitType.compareTo(b.unitType);
}

bool _areUnitsEquivalent(EditableGridUnit a, EditableGridUnit b) =>
    a.unitType == b.unitType && a.gridX == b.gridX && a.gridY == b.gridY && haveEquivalentLinks(a, b) && haveEquivalentConfig(a, b);

bool areUnitCollectionsEqual(List<EditableGridUnit> left, List<EditableGridUnit> right) {
  if (left.length != right.length) return false;
  final sortedLeft = [...left]..sort(_compareUnits);
  final sortedRight = [...right]..sort(_compareUnits);
  for (var i = 0; i < sortedLeft.length; i++) {
    if (!_areUnitsEquivalent(sortedLeft[i], sortedRight[i])) return false;
  }
  return true;
}

/// Draft-vs-baseline diffing that feeds the "before you save" summary
/// (ROADMAP: "Show draft-change summaries before committing") and the
/// upgrade-preview/upgrade-info flow's Save button label.
extension BuildingGridDraftControllerSummary on BuildingGridDraftController {
  bool get hasDraftChanges => !areUnitCollectionsEqual(draftUnits, editBaselineUnits) || draftUpgradeUnitIds.isNotEmpty;

  Map<String, EditableGridUnit> _byPosition(List<EditableGridUnit> units) => {for (final u in units) '${u.gridX},${u.gridY}': u};

  List<LinkChangeEntry> get draftLinkChanges {
    if (!isEditing) return const [];
    final baselineByPos = _byPosition(editBaselineUnits);
    final draftByPos = _byPosition(draftUnits);
    final positions = {...baselineByPos.keys, ...draftByPos.keys};
    final changes = <LinkChangeEntry>[];

    for (final pos in positions) {
      final baseline = baselineByPos[pos];
      final draft = draftByPos[pos];
      final parts = pos.split(',');
      final bx = int.parse(parts[0]);
      final by = int.parse(parts[1]);

      for (final dir in _linkDirections) {
        final wasActive = baseline != null && _flagValue(baseline, dir.flag);
        final isActive = draft != null && _flagValue(draft, dir.flag);
        if (wasActive == isActive) continue;

        final srcType = draft?.unitType ?? baseline?.unitType ?? '?';
        final targetUnit = draftByPos['${bx + dir.dx},${by + dir.dy}'] ?? baselineByPos['${bx + dir.dx},${by + dir.dy}'];
        final tgtType = targetUnit?.unitType ?? '?';
        changes.add(
          LinkChangeEntry(
            description: '$srcType ($bx,$by) ${dir.label} → $tgtType (${bx + dir.dx},${by + dir.dy})',
            added: isActive,
          ),
        );
      }
    }
    return changes;
  }

  List<UnitChangeEntry> get draftUnitChanges {
    if (!isEditing) return const [];
    final baselineByPos = _byPosition(editBaselineUnits);
    final draftByPos = _byPosition(draftUnits);
    final positions = {...baselineByPos.keys, ...draftByPos.keys};
    final entries = <UnitChangeEntry>[];

    for (final pos in positions) {
      final baseline = baselineByPos[pos];
      final draft = draftByPos[pos];
      if (baseline == null && draft != null) {
        entries.add(
          UnitChangeEntry(
            changeType: 'added',
            gridX: draft.gridX,
            gridY: draft.gridY,
            unitType: draft.unitType,
            ticks: unitPlanChangeTicks,
            cost: _round2(getUnitConstructionCost(draft.unitType) * cityFxRate),
          ),
        );
      } else if (baseline != null && draft == null) {
        entries.add(
          UnitChangeEntry(changeType: 'removed', gridX: baseline.gridX, gridY: baseline.gridY, unitType: baseline.unitType, ticks: unitPlanChangeTicks, cost: 0),
        );
      } else if (baseline != null && draft != null && baseline.unitType != draft.unitType) {
        entries.add(
          UnitChangeEntry(
            changeType: 'replaced',
            gridX: draft.gridX,
            gridY: draft.gridY,
            unitType: draft.unitType,
            previousUnitType: baseline.unitType,
            ticks: unitPlanChangeTicks,
            cost: _round2(getUnitConstructionCost(draft.unitType) * cityFxRate),
          ),
        );
      }
    }
    entries.sort((a, b) => a.gridY != b.gridY ? a.gridY - b.gridY : a.gridX - b.gridX);
    return entries;
  }

  /// Total construction cost, FX-converted to the city's local currency.
  /// The web computes this in raw unconverted EUR (a pre-existing display
  /// inconsistency vs. its own per-line `draftUnitChanges` costs, which
  /// *are* FX-converted) — fixed here for internal consistency rather than
  /// replicated, since a brand-new implementation shouldn't ship a known
  /// display bug. See `getPlannedUnitConstructionCost`'s doc comment.
  double get draftConstructionCost => _round2(sumPlannedConfigurationCost(activeUnits, draftUnits) * cityFxRate);

  /// Max ticks across every grid position touched by the draft relative to
  /// the active grid — see this file's header comment for the one edge
  /// case (editing an already-pending plan) this simplifies away.
  int get draftTotalTicks {
    final positions = <String>{};
    for (final u in activeUnits) {
      positions.add('${u.gridX},${u.gridY}');
    }
    for (final u in draftUnits) {
      positions.add('${u.gridX},${u.gridY}');
    }
    var maxTicks = 0;
    for (final pos in positions) {
      final parts = pos.split(',');
      final x = int.parse(parts[0]);
      final y = int.parse(parts[1]);
      final active = activeUnitAt(x, y);
      final draft = draftUnitAt(x, y);
      final ticks = draft != null ? calculateTicksRequired(active, draft) : (active != null ? unitPlanChangeTicks : 0);
      if (ticks > maxTicks) maxTicks = ticks;
    }
    return maxTicks;
  }

  double? get projectedCompanyCashAfterApply => hasCompanyCash ? companyCash - draftConstructionCost : null;
}

double _round2(double value) => (value * 100).round() / 100;
