part of 'building_grid_draft_controller.dart';

/// 8-directional link toggle wrappers — always operate on [draftUnits],
/// never [activeUnits], matching `toggleHorizontalLink`/etc in
/// `useBuildingDetail.ts`. The only guard is "both endpoint cells are
/// occupied" (see `canToggle*` in `building_link_helpers.dart`) — there is
/// no client-side pre-validation of the resulting link state beyond that;
/// bad combinations (contradictory links, etc.) are caught by the server on
/// save, exactly as on web.
extension BuildingGridDraftControllerLinks on BuildingGridDraftController {
  void toggleHorizontalLink(int x, int y) {
    if (!isEditing) return;
    final left = draftUnitAt(x, y);
    final right = draftUnitAt(x + 1, y);
    if (left == null || right == null) return;
    applyHorizontalLinkCycle(left, right, getHorizontalLinkState(draftUnits, x, y));
    notify();
  }

  void toggleVerticalLink(int x, int y) {
    if (!isEditing) return;
    final top = draftUnitAt(x, y);
    final bottom = draftUnitAt(x, y + 1);
    if (top == null || bottom == null) return;
    applyVerticalLinkCycle(top, bottom, getVerticalLinkState(draftUnits, x, y));
    notify();
  }

  void togglePrimaryDiagonalLink(int x, int y) {
    if (!isEditing) return;
    final topLeft = draftUnitAt(x, y);
    final bottomRight = draftUnitAt(x + 1, y + 1);
    if (topLeft == null || bottomRight == null) return;
    applyPrimaryDiagonalLinkCycle(topLeft, bottomRight, getPrimaryDiagonalLinkState(draftUnits, x, y));
    notify();
  }

  void toggleSecondaryDiagonalLink(int x, int y) {
    if (!isEditing) return;
    final topRight = draftUnitAt(x + 1, y);
    final bottomLeft = draftUnitAt(x, y + 1);
    if (topRight == null || bottomLeft == null) return;
    applySecondaryDiagonalLinkCycle(topRight, bottomLeft, getSecondaryDiagonalLinkState(draftUnits, x, y));
    notify();
  }
}
