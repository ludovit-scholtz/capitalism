// Persistent header indicator showing the active context's balance (person
// or company, mirroring `ContextSwitcher`/`AccountContextState`) and a live
// tick-progress bar, mirroring the web's `GameTimeChip.vue` +
// `stores/gameState.ts`. Always visible in `AppShell`'s app bar once signed
// in, on both wide and narrow layouts — only the `ContextSwitcher` control
// itself moves (into the drawer) on narrow screens; this stays put.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:provider/provider.dart';

import '../auth/auth_state.dart';
import '../context/account_context_service.dart';
import '../context/account_context_state.dart';
import '../game_state/game_state_service.dart';
import '../game_state/game_state_state.dart';
import '../graphql/graphql_service.dart';
import '../theme/app_icons.dart';
import '../theme/app_theme.dart';

class GameStatusBar extends StatefulWidget {
  const GameStatusBar({super.key, GameStateService? gameStateService, AccountContextService? accountContextService})
    : _injectedGameStateService = gameStateService,
      _injectedAccountContextService = accountContextService;

  final GameStateService? _injectedGameStateService;
  final AccountContextService? _injectedAccountContextService;

  @override
  State<GameStatusBar> createState() => _GameStatusBarState();
}

class _GameStatusBarState extends State<GameStatusBar> {
  late final GameStateService _gameStateService;
  late final AccountContextService _accountContextService;
  late final GameStateState _gameStateState;

  Timer? _progressTimer;
  double _progressPercent = 0;
  int? _lastRefreshedTick;
  bool _fetchingGameState = false;
  DateTime? _lastFetchAttemptAt;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthState>();
    final graphQlService = GraphQlService(auth);
    _gameStateService = widget._injectedGameStateService ?? GameStateService(graphQlService);
    _accountContextService = widget._injectedAccountContextService ?? AccountContextService(graphQlService);

    _gameStateState = context.read<GameStateState>();
    _gameStateState.addListener(_onGameStateChanged);

    // A single timer, owned by this State, drives both the 100ms
    // progress-bar animation and (when overdue) the next GraphQL refetch —
    // deliberately not a self-rescheduling `Timer` living on [GameStateState]
    // itself, so polling starts and stops with this widget's own lifecycle
    // (mount/dispose) rather than running in the background for the lifetime
    // of the app regardless of whether anything is displaying it.
    _progressTimer = Timer.periodic(const Duration(milliseconds: 100), (_) => _onTick());
    WidgetsBinding.instance.addPostFrameCallback((_) => _maybeFetchGameState());
  }

  @override
  void dispose() {
    _gameStateState.removeListener(_onGameStateChanged);
    _progressTimer?.cancel();
    super.dispose();
  }

  /// Refetches the active balance whenever the server's tick advances,
  /// mirroring the web's `useTickRefresh` pattern. Reuses
  /// `AccountContextState.refresh` (rather than a bespoke balance-only
  /// query) so this stays in sync with the same account/company/city data
  /// the context switcher shows.
  void _onGameStateChanged() {
    final tick = _gameStateState.gameState?.currentTick;
    if (tick == null || tick == _lastRefreshedTick) return;
    _lastRefreshedTick = tick;
    unawaited(context.read<AccountContextState>().refresh(_accountContextService));
  }

  void _onTick() {
    if (!mounted) return;
    _updateProgress();
    _maybeFetchGameState();
  }

  void _updateProgress() {
    final gameState = _gameStateState.gameState;
    final lastTickAtUtc = gameState?.lastTickAtUtc;
    if (gameState == null || lastTickAtUtc == null) {
      if (_progressPercent != 0) setState(() => _progressPercent = 0);
      return;
    }

    final intervalSeconds = gameState.tickIntervalSeconds > 0 ? gameState.tickIntervalSeconds : 10;
    final elapsedMs = DateTime.now().toUtc().difference(lastTickAtUtc).inMilliseconds;
    final next = (elapsedMs / (intervalSeconds * 1000)).clamp(0.0, 1.0);
    if ((next - _progressPercent).abs() > 0.001) {
      setState(() => _progressPercent = next);
    }
  }

  /// Refetches once no game state has loaded yet, or once the server's next
  /// tick is due (+250ms grace, mirroring `scheduleNextRefresh` on the web).
  /// Guarded by an in-flight flag plus a 1s minimum gap between attempts so
  /// an overdue tick doesn't spam requests every 100ms while waiting for the
  /// server to actually advance.
  void _maybeFetchGameState() {
    if (_fetchingGameState) return;

    final gameState = _gameStateState.gameState;
    final lastTickAtUtc = gameState?.lastTickAtUtc;
    if (gameState != null && lastTickAtUtc != null) {
      final intervalMs = (gameState.tickIntervalSeconds > 0 ? gameState.tickIntervalSeconds : 10) * 1000;
      final dueAt = lastTickAtUtc.add(Duration(milliseconds: intervalMs.round() + 250));
      if (DateTime.now().toUtc().isBefore(dueAt)) return;
    }

    final now = DateTime.now();
    final lastAttempt = _lastFetchAttemptAt;
    if (lastAttempt != null && now.difference(lastAttempt) < const Duration(seconds: 1)) return;
    _lastFetchAttemptAt = now;

    _fetchingGameState = true;
    unawaited(
      _gameStateState.refresh(_gameStateService, force: true).whenComplete(() => _fetchingGameState = false),
    );
  }

  @override
  Widget build(BuildContext context) {
    final accountContext = context.watch<AccountContextState>();
    final gameState = context.watch<GameStateState>().gameState;

    final balance = accountContext.isCompanyMode
        ? accountContext.activeCompany?.cash
        : accountContext.activeAccount?.availableCash;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        _StatusChip(
          key: const Key('nav-balance-chip'),
          icon: AppIcons.wallet,
          label: balance != null ? '\$${balance.toStringAsFixed(0)}' : '—',
        ),
        const SizedBox(width: 8),
        _StatusChip(
          key: const Key('nav-tick-chip'),
          icon: AppIcons.clock,
          label: gameState != null ? 'Tick ${gameState.currentTick}' : '—',
          progressPercent: gameState != null ? _progressPercent : null,
        ),
      ],
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({super.key, required this.icon, required this.label, this.progressPercent});

  final FaIconData icon;
  final String label;

  /// When non-null, a filled bar behind the content shows elapsed progress
  /// toward the next tick (0-1), mirroring `GameTimeChip.vue`.
  final double? progressPercent;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 32,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: AppTheme.spaceSurfaceHigh,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: AppTheme.neonCyan.withValues(alpha: 0.25)),
      ),
      child: Stack(
        alignment: Alignment.centerLeft,
        children: [
          if (progressPercent != null)
            Positioned.fill(
              child: FractionallySizedBox(
                alignment: Alignment.centerLeft,
                widthFactor: progressPercent!.clamp(0.0, 1.0),
                child: Container(color: AppTheme.neonCyan.withValues(alpha: 0.18)),
              ),
            ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 10),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                FaIcon(icon, size: 12, color: AppTheme.neonCyan),
                const SizedBox(width: 6),
                Text(
                  label,
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(color: Colors.white),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
