import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/config/app_config.dart';
import '../../core/config/app_environment_state.dart';
import '../../core/config/game_server_state.dart';
import '../../core/services/app_logger.dart';
import '../../core/theme/app_icons.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/icon_badge.dart';

/// Reached from the About screen's "Dev info" button. Shows the app's
/// runtime configuration plus a live view of [AppLogger]'s in-memory log
/// buffer, so a player hitting a generic error (e.g. "Could not load the
/// news feed. Please try again.") can see *why* it failed — the actual
/// HTTP/GraphQL error — without a connected debugger, and copy it into a
/// bug report.
class DevInfoScreen extends StatefulWidget {
  const DevInfoScreen({super.key});

  @override
  State<DevInfoScreen> createState() => _DevInfoScreenState();
}

class _DevInfoScreenState extends State<DevInfoScreen> {
  PackageInfo? _packageInfo;

  @override
  void initState() {
    super.initState();
    PackageInfo.fromPlatform().then((info) {
      if (mounted) setState(() => _packageInfo = info);
    });
  }

  Future<void> _copyLogs() async {
    await Clipboard.setData(ClipboardData(text: _buildReport()));
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Debug info copied to clipboard.')));
  }

  Future<void> _clearLogs() async {
    AppLogger.instance.clear();
  }

  String _buildReport() {
    final auth = context.read<AuthState>();
    final environment = context.read<AppEnvironmentState>().environment;
    final gameServer = context.read<GameServerState>();
    final info = _packageInfo;
    final buffer = StringBuffer()
      ..writeln('Capitalism 5 — Dev info')
      ..writeln('App version: ${info == null ? 'unknown' : '${info.version} (${info.buildNumber})'}')
      ..writeln('Platform: ${Platform.operatingSystem} ${Platform.operatingSystemVersion}')
      ..writeln('Environment: ${environment.label}')
      ..writeln('Game server: ${gameServer.selectedDisplayName ?? 'not connected'} (${AppConfig.graphqlUrl})')
      ..writeln('Master API: ${AppConfig.masterGraphqlUrl}')
      ..writeln('Authenticated: ${auth.isAuthenticated} (admin: ${auth.isAdmin})')
      ..writeln('---- log ----')
      ..writeln(AppLogger.instance.exportAsText());
    return buffer.toString();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final auth = context.watch<AuthState>();
    final environment = context.watch<AppEnvironmentState>().environment;
    final gameServer = context.watch<GameServerState>();

    return AnimatedBuilder(
      animation: AppLogger.instance,
      builder: (context, _) {
        final entries = AppLogger.instance.entries.toList().reversed.toList();

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.lg, AppSpacing.lg, AppSpacing.sm),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const SectionHeading(icon: AppIcons.devInfo, title: 'Dev info'),
                  const SizedBox(height: AppSpacing.md),
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _InfoLine('Environment', environment.label),
                          _InfoLine(
                            'Game server',
                            '${gameServer.selectedDisplayName ?? 'not connected'} — ${AppConfig.graphqlUrl.isEmpty ? '(none)' : AppConfig.graphqlUrl}',
                          ),
                          _InfoLine('Master API', AppConfig.masterGraphqlUrl),
                          _InfoLine('Authenticated', '${auth.isAuthenticated} (admin: ${auth.isAdmin})'),
                          _InfoLine('Platform', '${Platform.operatingSystem} ${Platform.operatingSystemVersion}'),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),
                  Row(
                    children: [
                      Expanded(child: Text('Log (${entries.length} entries)', style: theme.textTheme.titleSmall)),
                      IconButton(
                        tooltip: 'Copy debug info',
                        onPressed: _copyLogs,
                        icon: const FaIcon(AppIcons.copy, size: 16),
                      ),
                      IconButton(
                        tooltip: 'Clear log',
                        onPressed: entries.isEmpty ? null : _clearLogs,
                        icon: const FaIcon(AppIcons.trash, size: 16),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            Expanded(
              child: entries.isEmpty
                  ? Center(
                      child: Text(
                        'No log entries yet — actions you take in the app (like loading a screen) will appear here.',
                        style: theme.textTheme.bodyMedium?.copyWith(color: AppTheme.mutedText),
                        textAlign: TextAlign.center,
                      ),
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.sm),
                      itemCount: entries.length,
                      itemBuilder: (context, index) {
                        final entry = entries[index];
                        return Padding(
                          padding: const EdgeInsets.symmetric(vertical: 2),
                          child: Text.rich(
                            TextSpan(
                              children: [
                                TextSpan(
                                  text: '${entry.formattedTime} ',
                                  style: const TextStyle(color: AppTheme.mutedText, fontFamily: 'monospace', fontSize: 11),
                                ),
                                TextSpan(
                                  text: '${entry.level.name.toUpperCase().padRight(7)} ',
                                  style: TextStyle(
                                    color: _colorFor(entry.level),
                                    fontFamily: 'monospace',
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                                TextSpan(
                                  text: entry.tag != null ? '[${entry.tag}] ' : '',
                                  style: const TextStyle(color: AppTheme.neonCyan, fontFamily: 'monospace', fontSize: 11),
                                ),
                                TextSpan(
                                  text: entry.message,
                                  style: const TextStyle(fontFamily: 'monospace', fontSize: 11),
                                ),
                              ],
                            ),
                          ),
                        );
                      },
                    ),
            ),
          ],
        );
      },
    );
  }

  Color _colorFor(LogLevel level) => switch (level) {
    LogLevel.debug => AppTheme.mutedText,
    LogLevel.info => AppTheme.neonGreen,
    LogLevel.warning => AppTheme.neonAmber,
    LogLevel.error => AppTheme.neonRed,
  };
}

class _InfoLine extends StatelessWidget {
  const _InfoLine(this.label, this.value);

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Text.rich(
        TextSpan(
          children: [
            TextSpan(text: '$label: ', style: theme.textTheme.bodySmall?.copyWith(color: AppTheme.mutedText)),
            TextSpan(text: value, style: theme.textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}
