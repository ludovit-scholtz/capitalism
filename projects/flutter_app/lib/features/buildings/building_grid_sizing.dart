// Shared cell/connector sizing for the 4x4 unit grid, used by both
// `BuildingUnitGrid` (read-only) and `BuildingGridEditor` (edit mode) so the
// left column of the responsive Building Detail layout scales the grid
// (cells + link/connector arrows together) to fit the available width on
// wide screens instead of always forcing horizontal scroll — mirrors the
// intent of the web's `min-width: 330px` + horizontal-scroll-below-640px
// rule, but scales down to fit first and only falls back to scrolling once
// cells would drop below a legible minimum.

const double kGridDefaultCellSize = 88;
const double kGridDefaultConnectorSize = 28;
const double kGridMinCellSize = 56;
const double kGridNaturalWidth = kGridDefaultCellSize * 4 + kGridDefaultConnectorSize * 3;

class GridSizing {
  const GridSizing({required this.cellSize, required this.connectorSize, required this.scrollable});

  final double cellSize;
  final double connectorSize;

  /// True when the grid couldn't shrink enough to fit `availableWidth`
  /// without dropping cells below [kGridMinCellSize] — falls back to the
  /// natural size wrapped in a horizontal scroll view.
  final bool scrollable;

  double get totalWidth => cellSize * 4 + connectorSize * 3;
}

/// Computes cell/connector sizes for the given available width, preserving
/// the natural `connectorSize / cellSize` ratio so link arrows keep their
/// proportions at any scale.
GridSizing computeGridSizing(double availableWidth) {
  if (!availableWidth.isFinite || availableWidth >= kGridNaturalWidth) {
    return const GridSizing(cellSize: kGridDefaultCellSize, connectorSize: kGridDefaultConnectorSize, scrollable: false);
  }
  const ratio = kGridDefaultConnectorSize / kGridDefaultCellSize;
  final cellSize = availableWidth / (4 + ratio * 3);
  if (cellSize < kGridMinCellSize) {
    return const GridSizing(cellSize: kGridDefaultCellSize, connectorSize: kGridDefaultConnectorSize, scrollable: true);
  }
  return GridSizing(cellSize: cellSize, connectorSize: cellSize * ratio, scrollable: false);
}
