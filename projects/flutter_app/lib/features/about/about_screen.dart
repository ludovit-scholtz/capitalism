import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:go_router/go_router.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../../core/theme/app_icons.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/icon_badge.dart';

/// Mobile-only addition — `projects/frontend` has no equivalent "About"
/// view. Shows the app identity/version and links to the Dev Info screen,
/// which surfaces the in-memory debug log (see `AppLogger`) so players can
/// self-diagnose failures (e.g. "Could not load the news feed") without a
/// connected debugger.
class AboutScreen extends StatefulWidget {
  const AboutScreen({super.key});

  @override
  State<AboutScreen> createState() => _AboutScreenState();
}

class _AboutScreenState extends State<AboutScreen> {
  PackageInfo? _packageInfo;

  @override
  void initState() {
    super.initState();
    PackageInfo.fromPlatform().then((info) {
      if (mounted) setState(() => _packageInfo = info);
    });
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final info = _packageInfo;

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: [
        Center(
          child: Column(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(AppRadius.lg),
                child: Image.asset('assets/icon/app_icon.png', width: 96, height: 96),
              ),
              const SizedBox(height: AppSpacing.md),
              Text('Capitalism 5', style: theme.textTheme.headlineSmall),
              const SizedBox(height: AppSpacing.xs),
              Text(
                'Multiplayer play-to-earn economic strategy',
                style: theme.textTheme.bodyMedium?.copyWith(color: AppTheme.mutedText),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.xl),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SectionHeading(icon: AppIcons.about, title: 'App info'),
                const SizedBox(height: AppSpacing.md),
                _InfoRow(label: 'Version', value: info == null ? '…' : '${info.version} (${info.buildNumber})'),
                _InfoRow(label: 'Package', value: info?.packageName ?? '…'),
              ],
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        OutlinedButton.icon(
          onPressed: () => context.push('/about/dev-info'),
          icon: const FaIcon(AppIcons.devInfo, size: 16),
          label: const Text('Dev info'),
        ),
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
      child: Row(
        children: [
          SizedBox(
            width: 90,
            child: Text(label, style: theme.textTheme.bodySmall?.copyWith(color: AppTheme.mutedText)),
          ),
          Expanded(child: Text(value, style: theme.textTheme.bodyMedium)),
        ],
      ),
    );
  }
}
