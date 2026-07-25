// Lightweight first-time-user coachmark overlay, the mobile equivalent of
// `projects/frontend/src/components/ui/TutorialTooltip.vue`. Built from
// scratch (ROADMAP 138) — no existing coachmark/showcase mechanism exists
// anywhere else in this app to reuse. Deliberately backend-milestone-driven
// only, with no session-storage guest fallback: unlike the web (which must
// support guests via `useFirstTimeUserGates.ts`'s sessionStorage path),
// Building Detail already requires an authenticated, onboarded player, so
// there's no guest case to cover. Auto-dismisses after 30s of inactivity
// exactly like web; the caller (`BuildingDetailScreen`) owns sequencing
// (building-detail tooltip, then grid-editor tooltip) and persistence
// (`TutorialService.markComplete`).

import 'dart:async';

import 'package:flutter/material.dart';

import '../theme/app_spacing.dart';

class TutorialTooltipCard extends StatefulWidget {
  const TutorialTooltipCard({super.key, required this.title, required this.body, required this.onDismiss});

  final String title;
  final String body;
  final VoidCallback onDismiss;

  @override
  State<TutorialTooltipCard> createState() => _TutorialTooltipCardState();
}

class _TutorialTooltipCardState extends State<TutorialTooltipCard> {
  Timer? _autoDismissTimer;

  @override
  void initState() {
    super.initState();
    _autoDismissTimer = Timer(const Duration(seconds: 30), widget.onDismiss);
  }

  @override
  void dispose() {
    _autoDismissTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Align(
      alignment: Alignment.bottomCenter,
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Semantics(
            liveRegion: true,
            child: Card(
              elevation: 8,
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.md),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Padding(padding: EdgeInsets.only(top: 2), child: Text('💡', style: TextStyle(fontSize: 20))),
                    const SizedBox(width: AppSpacing.sm),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(widget.title, style: theme.textTheme.titleSmall),
                          const SizedBox(height: 4),
                          Text(widget.body, style: theme.textTheme.bodySmall),
                          const SizedBox(height: AppSpacing.sm),
                          Align(
                            alignment: Alignment.centerRight,
                            child: FilledButton(onPressed: widget.onDismiss, child: const Text('Got it')),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
