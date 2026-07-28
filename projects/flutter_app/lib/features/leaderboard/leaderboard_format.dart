// Shared formatting helper for the Leaderboard and Player Profile screens.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/locale_state.dart';
import '../../core/utils/app_number_format.dart';

/// Locale-aware compact USD wealth display — mirrors `formatCompactMoney` on
/// the web. Threaded through `context` (rather than a plain `languageCode`
/// param) since every call site already has one available in its own
/// `build`.
String formatCompactWealth(BuildContext context, double value) =>
    AppNumberFormat.compactMoney(value, languageCode: context.watch<LocaleState>().languageCode);
