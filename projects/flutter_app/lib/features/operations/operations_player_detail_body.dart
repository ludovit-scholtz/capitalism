// Player detail body for the Operations Players/PlayerDetail screens,
// ported from `projects/frontend/src/views/OperationsPlayerDetailView.vue`
// — now includes the admin actions the web exposes: impersonation,
// chat-visibility toggling, and (root administrators only) local/global
// admin-role grants.

import 'package:flutter/material.dart';

import '../../core/auth/auth_state.dart';
import 'operations_models.dart';
import 'operations_service.dart';

class PlayerDetailBody extends StatefulWidget {
  const PlayerDetailBody({
    super.key,
    required this.player,
    required this.service,
    required this.authState,
    required this.isRootAdministrator,
    required this.onChanged,
  });

  final GameAdminPlayer player;
  final OperationsService service;
  final AuthState authState;
  final bool isRootAdministrator;

  /// Called after any action that changed server state, so the caller can
  /// refetch the dashboard (and — for impersonation — the new session is
  /// already applied via `authState.setToken` by the time this fires).
  final VoidCallback onChanged;

  @override
  State<PlayerDetailBody> createState() => _PlayerDetailBodyState();
}

class _PlayerDetailBodyState extends State<PlayerDetailBody> {
  bool _busy = false;

  Future<void> _impersonate() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text('Impersonate ${widget.player.displayName}?'),
        content: const Text('This replaces your current session with a session logged in as this player. Use "Stop" in the banner to return to your own account.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Impersonate')),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _busy = true);
    try {
      final token = await widget.service.startImpersonation(targetPlayerId: widget.player.id);
      await widget.authState.setToken(token);
      widget.onChanged();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not start impersonation.')));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _toggleChatVisibility(bool isInvisible) async {
    setState(() => _busy = true);
    try {
      await widget.service.setPlayerInvisibleInChat(playerId: widget.player.id, isInvisible: isInvisible);
      widget.onChanged();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not update chat visibility.')));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _toggleLocalAdmin(bool isAdmin) async {
    setState(() => _busy = true);
    try {
      await widget.service.setLocalGameAdminRole(playerId: widget.player.id, isAdmin: isAdmin);
      widget.onChanged();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not update the admin role.')));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _manageGlobalAdmin(bool grant) async {
    setState(() => _busy = true);
    try {
      if (grant) {
        await widget.service.assignGlobalGameAdminRole(widget.player.email);
      } else {
        await widget.service.removeGlobalGameAdminRole(widget.player.email);
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(grant ? 'Global admin role granted.' : 'Global admin role removed.')));
      }
      widget.onChanged();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not update the global admin role.')));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final player = widget.player;
    final theme = Theme.of(context);
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(player.displayName, style: theme.textTheme.headlineSmall),
        Text(player.email, style: theme.textTheme.bodyMedium),
        const SizedBox(height: 16),
        Text('Role: ${player.role}'),
        Text('Personal cash: ${player.personalCash.toStringAsFixed(0)}'),
        Text('Company cash: ${player.totalCompanyCash.toStringAsFixed(0)}'),
        Text('Companies: ${player.companyCount}'),
        Text('Cities: ${player.cityNames.isEmpty ? '—' : player.cityNames.join(', ')}'),
        Text('Last login: ${player.lastLoginAtUtc ?? 'Never'}'),
        const SizedBox(height: 20),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Actions', style: theme.textTheme.titleSmall),
                const SizedBox(height: 8),
                OutlinedButton(
                  key: const ValueKey('impersonate-button'),
                  onPressed: _busy ? null : _impersonate,
                  child: const Text('Impersonate this player'),
                ),
                const SizedBox(height: 8),
                SwitchListTile(
                  key: const ValueKey('invisible-in-chat-switch'),
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Invisible in chat'),
                  value: player.isInvisibleInChat,
                  onChanged: _busy ? null : _toggleChatVisibility,
                ),
                if (widget.isRootAdministrator) ...[
                  const Divider(),
                  Text('Root administrator actions', style: theme.textTheme.labelLarge),
                  SwitchListTile(
                    key: const ValueKey('local-admin-switch'),
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Local server admin'),
                    value: player.role == 'ADMIN',
                    onChanged: _busy ? null : _toggleLocalAdmin,
                  ),
                  const SizedBox(height: 4),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          key: const ValueKey('grant-global-admin-button'),
                          onPressed: _busy ? null : () => _manageGlobalAdmin(true),
                          child: const Text('Grant global admin'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: OutlinedButton(
                          key: const ValueKey('revoke-global-admin-button'),
                          onPressed: _busy ? null : () => _manageGlobalAdmin(false),
                          child: const Text('Revoke global admin'),
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ),
      ],
    );
  }
}
