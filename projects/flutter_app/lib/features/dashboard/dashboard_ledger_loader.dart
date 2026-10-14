import 'dart:async';

import 'package:flutter/foundation.dart';

import '../company/company_models.dart';
import 'dashboard_service.dart';

/// Loads the dashboard's per-company financial summaries (`companyLedger`).
///
/// Companies load **in parallel** and each card updates as soon as its own
/// ledger arrives, so one slow company never delays (or hides) the others.
/// Listeners (the dashboard screen) rebuild whenever any state changes.
///
/// Rules that keep the UI calm when the server is slow:
/// * A *user-initiated* load (first open, Retry) shows the skeleton and
///   supersedes anything older; its result replaces stale numbers.
/// * A *silent* load (server tick, pull-to-refresh) never starts a second
///   request while one is running for that company (so slow responses can't
///   pile up behind every tick), and never flips a card that already has an
///   outcome (data, "no data" or an error) back into a skeleton.
/// * A response is applied only if it belongs to the company's newest request.
class DashboardLedgerLoader extends ChangeNotifier {
  DashboardLedgerLoader(this._service);

  final DashboardService _service;

  Map<String, CompanyLedger> _ledgers = {};
  final Set<String> _loading = {};
  final Set<String> _failed = {};

  /// Companies whose latest request has produced *an outcome* at least once
  /// (data, no data, or a failure). Only companies not in here get a skeleton
  /// during a silent refresh.
  final Set<String> _settled = {};
  final Set<String> _inFlight = {};
  final Map<String, int> _requestSeq = {};

  /// Monotonic across all companies, so a company that leaves and later
  /// re-enters the list can never reuse a number an old in-flight request holds.
  int _requestCounter = 0;
  bool _disposed = false;

  /// Loaded ledgers by company id.
  Map<String, CompanyLedger> get ledgers => _ledgers;

  /// Companies whose first/user-initiated request is running (show a skeleton).
  Set<String> get loadingIds => _loading;

  /// Companies whose request failed (show an error with Retry).
  Set<String> get failedIds => _failed;

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  /// Loads every company's ledger in parallel. [silent] keeps what is on
  /// screen (see the class comment).
  Future<void> loadAll(List<String> companyIds, {bool silent = false}) async {
    if (_disposed) return;
    final ids = companyIds.toSet();
    // Forget everything about companies that no longer exist (e.g. merged away).
    _ledgers = {for (final entry in _ledgers.entries) if (ids.contains(entry.key)) entry.key: entry.value};
    _loading.retainAll(ids);
    _failed.retainAll(ids);
    _settled.retainAll(ids);
    _inFlight.retainAll(ids);
    _requestSeq.removeWhere((id, _) => !ids.contains(id));
    _notify();
    await Future.wait([for (final id in ids) load(id, silent: silent)]);
  }

  /// Loads one company's ledger (see the class comment for [silent]).
  Future<void> load(String companyId, {bool silent = false}) async {
    if (_disposed) return;
    if (silent && _inFlight.contains(companyId)) return;

    final seq = _requestSeq[companyId] = ++_requestCounter;
    _inFlight.add(companyId);
    if (!silent || !_settled.contains(companyId)) {
      _loading.add(companyId);
      _failed.remove(companyId);
      _notify();
    }
    try {
      final ledger = await _service.fetchCompanyOverviewLedger(companyId);
      if (_disposed || _requestSeq[companyId] != seq) return;
      _inFlight.remove(companyId);
      // A null ledger (company missing / no longer theirs) leaves the card on "No financial data yet."
      if (ledger != null) {
        _ledgers = {..._ledgers, companyId: ledger};
      } else {
        _ledgers = {for (final entry in _ledgers.entries) if (entry.key != companyId) entry.key: entry.value};
      }
      _failed.remove(companyId);
      _loading.remove(companyId);
      _settled.add(companyId);
      _notify();
    } catch (_) {
      if (_disposed || _requestSeq[companyId] != seq) return;
      _inFlight.remove(companyId);
      _loading.remove(companyId);
      // Keep showing existing numbers if there are any; otherwise surface the error.
      if (!_ledgers.containsKey(companyId)) _failed.add(companyId);
      _settled.add(companyId);
      _notify();
    }
  }
}
