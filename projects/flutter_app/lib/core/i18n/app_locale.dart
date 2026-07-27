/// App-level language support, mirroring `SUPPORTED_LOCALES`/`LOCALE_MAP` in
/// `projects/frontend/src/i18n/index.ts` and
/// `projects/frontend/src/lib/currencyFormat.ts` so both clients offer the
/// same language choices and format numbers/dates the same way for a given
/// choice.
library;

/// App language codes, in the same order the web's language switcher shows
/// them.
const List<String> kSupportedAppLanguages = ['en', 'sk', 'de'];

const String kDefaultAppLanguage = 'en';

const Map<String, String> _intlLocaleByLanguage = {'en': 'en_US', 'sk': 'sk_SK', 'de': 'de_DE'};

/// Human-readable language names, matching `languages.*` in
/// `projects/frontend/src/i18n/locales/en.ts`.
const Map<String, String> languageDisplayNames = {'en': 'English', 'sk': 'Slovenčina', 'de': 'Deutsch'};

/// Resolves an app language code to the locale identifier `intl`'s
/// `NumberFormat`/`DateFormat` expect, matching `LOCALE_MAP` on the web.
String resolveIntlLocale(String languageCode) =>
    _intlLocaleByLanguage[languageCode] ?? _intlLocaleByLanguage[kDefaultAppLanguage]!;
