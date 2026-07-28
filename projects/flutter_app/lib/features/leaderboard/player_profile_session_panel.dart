// Session-security panel for the Player Profile screen (own profile
// only), ported from the "Session security" section of
// `projects/frontend/src/views/PlayerProfileView.vue` — lists active login
// sessions from the game API's `/auth/sessions` REST endpoint and offers a
// "Log out other devices" action (`/auth/logout-all`).

import 'package:flutter/material.dart';

import 'leaderboard_models.dart';
import 'player_profile_service.dart';

class PlayerProfileSessionPanel extends StatefulWidget {
  const PlayerProfileSessionPanel({super.key, required this.service});

  final PlayerProfileService service;

  @override
  State<PlayerProfileSessionPanel> createState() => _PlayerProfileSessionPanelState();
}

class _PlayerProfileSessionPanelState extends State<PlayerProfileSessionPanel> {
  bool _loading = true;
  String? _error;
  List<PlayerSession> _sessions = const [];
  bool _logoutAllLoading = false;
  bool _logoutAllSuccess = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final sessions = await widget.service.fetchSessions();
      if (!mounted) return;
      setState(() {
        _sessions = sessions;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not load active sessions.';
        _loading = false;
      });
    }
  }

  Future<void> _logoutAllDevices() async {
    setState(() {
      _logoutAllLoading = true;
      _logoutAllSuccess = false;
      _error = null;
    });
    try {
      await widget.service.logoutAllDevices();
      if (!mounted) return;
      setState(() => _logoutAllSuccess = true);
      await _load();
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'Could not log out other devices.');
    } finally {
      if (mounted) setState(() => _logoutAllLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text('Session security', style: theme.textTheme.titleSmall)),
                OutlinedButton(
                  key: const ValueKey('logout-all-devices-button'),
                  onPressed: _logoutAllLoading ? null : _logoutAllDevices,
                  child: Text(_logoutAllLoading ? 'Loading…' : 'Log out other devices'),
                ),
              ],
            ),
            if (_logoutAllSuccess) const Padding(padding: EdgeInsets.only(top: 4), child: Text('✓ Other sessions have been logged out.', style: TextStyle(color: Colors.green))),
            const SizedBox(height: 8),
            if (_loading)
              const Center(child: Padding(padding: EdgeInsets.symmetric(vertical: 12), child: CircularProgressIndicator()))
            else if (_error != null)
              Text(_error!, style: TextStyle(color: theme.colorScheme.error))
            else if (_sessions.isEmpty)
              const Text('No active sessions found.')
            else
              for (final session in _sessions)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(border: Border.all(color: theme.colorScheme.outlineVariant), borderRadius: BorderRadius.circular(8)),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(child: Text(session.device, style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600))),
                            if (session.isCurrent) Text('Current session', style: TextStyle(color: theme.colorScheme.primary, fontSize: 11)),
                          ],
                        ),
                        Text('IP: ${session.ipAddress}', style: theme.textTheme.labelSmall),
                        Text('Last seen: ${session.lastSeenAtUtc}', style: theme.textTheme.labelSmall),
                      ],
                    ),
                  ),
                ),
          ],
        ),
      ),
    );
  }
}
