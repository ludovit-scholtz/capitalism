/// In-game calendar time derived from a tick number, mirroring
/// `projects/frontend/src/lib/gameTime.ts`: the game epoch is
/// 2000-01-01T00:00:00Z and each tick represents exactly one in-game hour —
/// independent of `tickIntervalSeconds` (the real-world seconds between
/// ticks, a separate concept used only for the header's tick-progress bar).
library;

import 'package:intl/intl.dart';

import '../i18n/app_locale.dart';

const int kGameStartYear = 2000;
const int kTicksPerDay = 24;
const int kDaysPerYear = 365;
const int kTicksPerYear = kTicksPerDay * kDaysPerYear;

DateTime _gameEpochUtc() => DateTime.utc(kGameStartYear);

/// The absolute in-game UTC calendar time represented by [tick], matching
/// `computeInGameTimeUtcFromTick` on the web.
DateTime computeInGameTimeUtcFromTick(int tick) => _gameEpochUtc().add(Duration(hours: tick < 0 ? 0 : tick));

/// Formats an already-known in-game UTC time for display, honoring the
/// app's selected language for separators/month names/date ordering —
/// matching `formatInGameTime` on the web (numeric year, short month, 24h
/// time, UTC).
String formatInGameTime(DateTime gameTimeUtc, String languageCode) {
  final locale = resolveIntlLocale(languageCode);
  return DateFormat.yMMMd(locale).add_Hm().format(gameTimeUtc.toUtc());
}

/// Converts [tick] to in-game calendar time and formats it — matching
/// `formatGameTickTime` on the web. Use this wherever a raw tick number
/// would otherwise be shown to a player; the raw tick itself should only
/// surface as debug info (e.g. a tooltip), see `GameTickTime`.
String formatGameTickTime(int tick, String languageCode) =>
    formatInGameTime(computeInGameTimeUtcFromTick(tick), languageCode);
