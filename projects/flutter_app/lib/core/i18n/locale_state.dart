import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart' show Locale;

import 'app_locale.dart';
import 'locale_storage.dart';

/// Holds the player's chosen app language and keeps it in sync with
/// persisted storage — the runtime-switchable counterpart to
/// `AppEnvironmentState`, surfaced on the Settings screen. Defaults
/// explicitly to [kDefaultAppLanguage] rather than following the device
/// locale: this app deliberately avoids silent device-preference fallbacks
/// (see CLAUDE.md's dark-first theme rule for the same reasoning) so number
/// and date formatting stay predictable until the player makes an explicit
/// choice.
class LocaleState extends ChangeNotifier {
  LocaleState({SelectedLocaleStorage? storage}) : _storage = storage ?? SharedPreferencesSelectedLocaleStorage();

  final SelectedLocaleStorage _storage;

  Locale _locale = const Locale(kDefaultAppLanguage);
  Locale get locale => _locale;
  String get languageCode => _locale.languageCode;

  Future<void> restoreSelection() async {
    final saved = await _storage.read();
    if (saved == null || !kSupportedAppLanguages.contains(saved) || saved == _locale.languageCode) return;
    _locale = Locale(saved);
    notifyListeners();
  }

  Future<void> setLanguage(String languageCode) async {
    if (!kSupportedAppLanguages.contains(languageCode) || languageCode == _locale.languageCode) return;
    _locale = Locale(languageCode);
    await _storage.write(languageCode);
    notifyListeners();
  }
}
