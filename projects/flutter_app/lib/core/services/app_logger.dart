import 'dart:collection';
import 'dart:developer' as developer;

import 'package:flutter/foundation.dart';

enum LogLevel { debug, info, warning, error }

class LogEntry {
  LogEntry({required this.timestamp, required this.level, required this.message, this.tag});

  final DateTime timestamp;
  final LogLevel level;
  final String message;
  final String? tag;

  String get formattedTime {
    final t = timestamp.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    String three(int n) => n.toString().padLeft(3, '0');
    return '${two(t.hour)}:${two(t.minute)}:${two(t.second)}.${three(t.millisecond)}';
  }

  String get formatted {
    final levelLabel = level.name.toUpperCase().padRight(7);
    final tagLabel = tag != null ? '[$tag] ' : '';
    return '$formattedTime $levelLabel $tagLabel$message';
  }
}

/// App-wide in-memory log buffer, surfaced on the Dev Info screen
/// (`About` -> `Dev Info`) so players can self-diagnose failures — e.g.
/// "Could not load the news feed" — without a connected debugger. Nearly
/// all backend interaction funnels through [GraphQlService],
/// [PasswordResetService], and [BiatecOidcService], which log request/error
/// details here, plus global Flutter/platform error handlers wired in
/// `main.dart` — so this single buffer captures the overwhelming majority of
/// failure causes across the app.
///
/// A singleton (rather than a provided/injected service) so it can be
/// reached from anywhere — including places without a [BuildContext], like
/// [FlutterError.onError] — without threading it through every constructor.
class AppLogger extends ChangeNotifier {
  AppLogger._();

  static final AppLogger instance = AppLogger._();

  static const _maxEntries = 500;

  final Queue<LogEntry> _entries = Queue<LogEntry>();

  UnmodifiableListView<LogEntry> get entries => UnmodifiableListView(_entries);

  void log(String message, {LogLevel level = LogLevel.info, String? tag}) {
    final entry = LogEntry(timestamp: DateTime.now(), level: level, message: message, tag: tag);
    _entries.addLast(entry);
    while (_entries.length > _maxEntries) {
      _entries.removeFirst();
    }
    developer.log(message, name: tag ?? 'Capitalism', level: _developerLevel(level));
    notifyListeners();
  }

  void debug(String message, {String? tag}) => log(message, level: LogLevel.debug, tag: tag);

  void info(String message, {String? tag}) => log(message, level: LogLevel.info, tag: tag);

  void warning(String message, {String? tag}) => log(message, level: LogLevel.warning, tag: tag);

  void error(String message, [Object? error, StackTrace? stackTrace, String? tag]) {
    final buffer = StringBuffer(message);
    if (error != null) buffer.write(' — $error');
    log(buffer.toString(), level: LogLevel.error, tag: tag);
    if (stackTrace != null) {
      developer.log(stackTrace.toString(), name: tag ?? 'Capitalism');
    }
  }

  void clear() {
    _entries.clear();
    notifyListeners();
  }

  String exportAsText() => _entries.map((e) => e.formatted).join('\n');

  int _developerLevel(LogLevel level) => switch (level) {
    LogLevel.debug => 500,
    LogLevel.info => 800,
    LogLevel.warning => 900,
    LogLevel.error => 1000,
  };
}
