// Pure port of `projects/frontend/src/lib/linkHelpers.ts` — the 8-directional
// link state machine shared by the horizontal/vertical/diagonal connectors
// in the grid editor. Every grid unit carries 8 independent one-directional
// boolean flags (`linkUp`/`linkDown`/`linkLeft`/`linkRight`/`linkUpLeft`/
// `linkUpRight`/`linkDownLeft`/`linkDownRight`) — a link between two cells is
// encoded by up to two independent flags, one on each unit, pointing at
// each other. `'both'` is a legacy dead-end only reachable via old
// persisted data; clicking never intentionally produces it (a single click
// on a `'both'` link collapses straight to `'none'`).

import 'building_grid_models.dart';

enum LinkState { none, forward, backward, both }

const List<String> supplyOriginTypes = ['PURCHASE', 'MINING'];
const List<String> sinkTypes = ['PUBLIC_SALES', 'B2B_SALES'];

/// First-click direction inference shared by all 4 axes. Read top-to-bottom,
/// first match wins.
bool _firstIsForward(String? firstType, String? secondType) {
  if (firstType != null && supplyOriginTypes.contains(firstType)) return true;
  if (secondType != null && supplyOriginTypes.contains(secondType)) return false;
  if (secondType != null && sinkTypes.contains(secondType)) return true;
  if (firstType != null && sinkTypes.contains(firstType)) return false;
  return true;
}

EditableGridUnit? _unitAt(List<EditableGridUnit> units, int x, int y) {
  for (final unit in units) {
    if (unit.gridX == x && unit.gridY == y) return unit;
  }
  return null;
}

LinkState _pairState(bool hasForward, bool hasBackward) {
  if (hasForward && hasBackward) return LinkState.both;
  if (hasForward) return LinkState.forward;
  if (hasBackward) return LinkState.backward;
  return LinkState.none;
}

/// Pair `(x,y)` (left) <-> `(x+1,y)` (right).
LinkState getHorizontalLinkState(List<EditableGridUnit> units, int x, int y) {
  final left = _unitAt(units, x, y);
  final right = _unitAt(units, x + 1, y);
  return _pairState(left?.linkRight ?? false, right?.linkLeft ?? false);
}

/// Pair `(x,y)` (top) <-> `(x,y+1)` (bottom).
LinkState getVerticalLinkState(List<EditableGridUnit> units, int x, int y) {
  final top = _unitAt(units, x, y);
  final bottom = _unitAt(units, x, y + 1);
  return _pairState(top?.linkDown ?? false, bottom?.linkUp ?? false);
}

/// `\` diagonal of the 2x2 block rooted at `(x,y)`: top-left `(x,y)` <->
/// bottom-right `(x+1,y+1)`.
LinkState getPrimaryDiagonalLinkState(List<EditableGridUnit> units, int x, int y) {
  final topLeft = _unitAt(units, x, y);
  final bottomRight = _unitAt(units, x + 1, y + 1);
  return _pairState(topLeft?.linkDownRight ?? false, bottomRight?.linkUpLeft ?? false);
}

/// `/` diagonal of the 2x2 block rooted at `(x,y)`: top-right `(x+1,y)` <->
/// bottom-left `(x,y+1)`.
LinkState getSecondaryDiagonalLinkState(List<EditableGridUnit> units, int x, int y) {
  final topRight = _unitAt(units, x + 1, y);
  final bottomLeft = _unitAt(units, x, y + 1);
  return _pairState(topRight?.linkDownLeft ?? false, bottomLeft?.linkUpRight ?? false);
}

bool canToggleHorizontalLink(List<EditableGridUnit> units, int x, int y) =>
    _unitAt(units, x, y) != null && _unitAt(units, x + 1, y) != null;

bool canToggleVerticalLink(List<EditableGridUnit> units, int x, int y) =>
    _unitAt(units, x, y) != null && _unitAt(units, x, y + 1) != null;

bool canTogglePrimaryDiagonalLink(List<EditableGridUnit> units, int x, int y) =>
    _unitAt(units, x, y) != null && _unitAt(units, x + 1, y + 1) != null;

bool canToggleSecondaryDiagonalLink(List<EditableGridUnit> units, int x, int y) =>
    _unitAt(units, x + 1, y) != null && _unitAt(units, x, y + 1) != null;

/// 3-state cycle: `none -> defaultDirection -> otherDirection -> none`.
/// Mutates [first]/[second] in place, matching the web's direct-mutation
/// pattern. [current] must be the state read from [first]/[second] just
/// before calling (via the corresponding `getXLinkState`).
void _applyCycle({
  required EditableGridUnit first,
  required EditableGridUnit second,
  required LinkState current,
  required bool firstIsForward,
  required void Function(EditableGridUnit unit, bool value) setFirstFlag,
  required void Function(EditableGridUnit unit, bool value) setSecondFlag,
}) {
  final defaultDir = firstIsForward ? LinkState.forward : LinkState.backward;
  final altDir = defaultDir == LinkState.forward ? LinkState.backward : LinkState.forward;

  LinkState next;
  if (current == LinkState.none) {
    next = defaultDir;
  } else if (current == defaultDir) {
    next = altDir;
  } else {
    next = LinkState.none;
  }

  setFirstFlag(first, next == LinkState.forward);
  setSecondFlag(second, next == LinkState.backward);
}

void applyHorizontalLinkCycle(EditableGridUnit left, EditableGridUnit right, LinkState current) {
  _applyCycle(
    first: left,
    second: right,
    current: current,
    firstIsForward: _firstIsForward(left.unitType, right.unitType),
    setFirstFlag: (u, v) => u.linkRight = v,
    setSecondFlag: (u, v) => u.linkLeft = v,
  );
}

void applyVerticalLinkCycle(EditableGridUnit top, EditableGridUnit bottom, LinkState current) {
  _applyCycle(
    first: top,
    second: bottom,
    current: current,
    firstIsForward: _firstIsForward(top.unitType, bottom.unitType),
    setFirstFlag: (u, v) => u.linkDown = v,
    setSecondFlag: (u, v) => u.linkUp = v,
  );
}

void applyPrimaryDiagonalLinkCycle(EditableGridUnit topLeft, EditableGridUnit bottomRight, LinkState current) {
  _applyCycle(
    first: topLeft,
    second: bottomRight,
    current: current,
    firstIsForward: _firstIsForward(topLeft.unitType, bottomRight.unitType),
    setFirstFlag: (u, v) => u.linkDownRight = v,
    setSecondFlag: (u, v) => u.linkUpLeft = v,
  );
}

void applySecondaryDiagonalLinkCycle(EditableGridUnit topRight, EditableGridUnit bottomLeft, LinkState current) {
  _applyCycle(
    first: topRight,
    second: bottomLeft,
    current: current,
    firstIsForward: _firstIsForward(topRight.unitType, bottomLeft.unitType),
    setFirstFlag: (u, v) => u.linkDownLeft = v,
    setSecondFlag: (u, v) => u.linkUpRight = v,
  );
}

/// Clears the 8 neighbor units' flags pointing into `(x,y)`, used before
/// removing the unit at that position.
void clearConnectionsAround(List<EditableGridUnit> units, int x, int y) {
  final left = _unitAt(units, x - 1, y);
  final right = _unitAt(units, x + 1, y);
  final up = _unitAt(units, x, y - 1);
  final down = _unitAt(units, x, y + 1);
  final upLeft = _unitAt(units, x - 1, y - 1);
  final upRight = _unitAt(units, x + 1, y - 1);
  final downLeft = _unitAt(units, x - 1, y + 1);
  final downRight = _unitAt(units, x + 1, y + 1);

  if (left != null) left.linkRight = false;
  if (right != null) right.linkLeft = false;
  if (up != null) up.linkDown = false;
  if (down != null) down.linkUp = false;
  if (upLeft != null) upLeft.linkDownRight = false;
  if (upRight != null) upRight.linkDownLeft = false;
  if (downLeft != null) downLeft.linkUpRight = false;
  if (downRight != null) downRight.linkUpLeft = false;
}
