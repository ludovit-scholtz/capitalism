// First-time-user tooltip sequencing for Building Detail (ROADMAP 138b),
// split out of `building_detail_screen.dart` to keep that file under the
// repo's 500-line guideline. Wraps the screen's content and owns all
// tutorial-milestone state itself — the screen only needs to tell it
// whether edit mode / a grid-eligible building is active.
//
// Sequencing mirrors `BuildingDetailView.vue`'s `onMounted` tooltip setup:
// the building-detail tooltip shows first (once `FIRST_BUILDING_DETAIL_VISIT`
// is fetched as incomplete, after an 800ms delay so it never flashes in
// before the building itself has rendered); dismissing it marks the
// milestone complete and only then can the grid-editor tooltip show, gated
// additionally on edit mode being active for a grid-eligible building.

import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/widgets/tutorial_tooltip.dart';
import '../tutorial/tutorial_models.dart';
import '../tutorial/tutorial_service.dart';

class BuildingDetailTutorialOverlay extends StatefulWidget {
  const BuildingDetailTutorialOverlay({
    super.key,
    required this.tutorialService,
    required this.isEditing,
    required this.isGridBuilding,
    required this.child,
  });

  final TutorialService tutorialService;
  final bool isEditing;
  final bool isGridBuilding;
  final Widget child;

  @override
  State<BuildingDetailTutorialOverlay> createState() => _BuildingDetailTutorialOverlayState();
}

class _BuildingDetailTutorialOverlayState extends State<BuildingDetailTutorialOverlay> {
  List<TutorialMilestoneStatus> _milestones = const [];
  bool _ready = false;
  final Set<String> _dismissed = {};
  Timer? _readyTimer;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    // A cancellable `Timer` rather than `await Future.delayed(...)` — the
    // latter leaves a pending timer flagged by `flutter_test` as a leak if
    // this widget is disposed before it fires.
    _readyTimer?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final milestones = await widget.tutorialService.fetchProgress();
      if (mounted) setState(() => _milestones = milestones);
    } catch (_) {
      // Non-critical — tooltips just won't show this session.
    }
    _readyTimer = Timer(const Duration(milliseconds: 800), () {
      if (mounted) setState(() => _ready = true);
    });
  }

  bool _isMilestoneDone(String milestone) =>
      _dismissed.contains(milestone) || _milestones.any((m) => m.milestone == milestone && m.isCompleted);

  Future<void> _dismiss(String milestone) async {
    setState(() => _dismissed.add(milestone));
    try {
      await widget.tutorialService.markComplete(milestone);
    } catch (_) {
      // Best-effort, matching web's `useFirstTimeUserGates.ts` — a failed
      // mark shouldn't re-show a tooltip the player already dismissed.
    }
  }

  @override
  Widget build(BuildContext context) {
    final showBuildingDetailTooltip = _ready && !_isMilestoneDone('FIRST_BUILDING_DETAIL_VISIT');
    final showGridEditorTooltip =
        _ready &&
        !showBuildingDetailTooltip &&
        widget.isEditing &&
        widget.isGridBuilding &&
        !_isMilestoneDone('FIRST_GRID_EDITOR_OPEN');

    return Stack(
      children: [
        widget.child,
        if (showBuildingDetailTooltip)
          TutorialTooltipCard(
            title: 'Building Detail View',
            body: 'This is where you configure and monitor everything about a building — units, links, and analytics.',
            onDismiss: () => _dismiss('FIRST_BUILDING_DETAIL_VISIT'),
          ),
        if (showGridEditorTooltip)
          TutorialTooltipCard(
            title: 'Unit Grid Editor',
            body: 'Tap an empty cell to place a unit, tap an occupied one to configure it, and use the connectors to link units together.',
            onDismiss: () => _dismiss('FIRST_GRID_EDITOR_OPEN'),
          ),
      ],
    );
  }
}
