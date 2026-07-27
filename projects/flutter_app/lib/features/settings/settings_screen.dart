// New screen — the web frontend has no dedicated settings page (its
// language switcher lives in `AppFooter.vue` instead). Mobile gets its own
// Settings screen (alongside the existing About screen) as the single place
// to change app-wide client preferences; currently just the app language,
// which drives every locale-aware number/date format in the app (see
// `AppNumberFormat`, `game_time.dart`) via `LocaleState`.

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/i18n/app_locale.dart';
import '../../core/i18n/locale_state.dart';
import '../../core/theme/app_icons.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/icon_badge.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final localeState = context.watch<LocaleState>();

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SectionHeading(icon: AppIcons.language, title: 'Language'),
                const SizedBox(height: AppSpacing.md),
                Text(
                  'Changes how numbers, currency amounts, and in-game dates are '
                  'formatted throughout the app.',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                const SizedBox(height: AppSpacing.md),
                SegmentedButton<String>(
                  key: const Key('settings-language-picker'),
                  segments: [
                    for (final code in kSupportedAppLanguages)
                      ButtonSegment(value: code, label: Text(languageDisplayNames[code] ?? code)),
                  ],
                  selected: {localeState.languageCode},
                  onSelectionChanged: (selection) => context.read<LocaleState>().setLanguage(selection.first),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
