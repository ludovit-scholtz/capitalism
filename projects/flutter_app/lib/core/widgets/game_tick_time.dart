/// Shows a tick as human-readable in-game time; the raw tick number is only
/// surfaced via a long-press/hover tooltip, for debugging — mirrors the
/// web's preference for showing `currentGameTimeUtc` over raw tick numbers
/// in player-facing UI (`GameTimeChip.vue`). Use this (or [formatGameTickTime]
/// directly, when a raw string is needed inside a larger `Text`) wherever a
/// tick number would otherwise be shown to a player.
library;

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../i18n/locale_state.dart';
import '../utils/game_time.dart';

class GameTickTime extends StatelessWidget {
  const GameTickTime(this.tick, {super.key, this.style, this.prefix = ''});

  final int tick;
  final TextStyle? style;

  /// Optional text shown before the formatted time, e.g. a label a caller
  /// wants to keep (`'Recorded '`).
  final String prefix;

  @override
  Widget build(BuildContext context) {
    final languageCode = context.watch<LocaleState>().languageCode;
    return Tooltip(
      message: 'Tick $tick',
      child: Text('$prefix${formatGameTickTime(tick, languageCode)}', style: style),
    );
  }
}
