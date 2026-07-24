/// Data models for the persistent person/company/city context switcher,
/// mirroring `projects/frontend/src/components/layout/ContextSwitcher.vue`
/// (+ `ContextSwitcherPanel.vue`, `lib/accountContext.ts`, `lib/cityContext.ts`).
/// GraphQL field names verified against `Api/Types/Inputs.cs`'s
/// `SwitchAccountContextInput` and `Api/Data/Entities/Player.cs`'s
/// `AccountContextType` (`"PERSON"` / `"COMPANY"`).
library;

/// One of the player's companies, as offered in the account-switcher list.
/// [buildingCityIds] has one entry per building (not deduplicated), so
/// callers can count occurrences to drive the "which city has the most of
/// my stuff" auto-select heuristic, matching the web's `selectMainCity`.
class ContextCompanyOption {
  const ContextCompanyOption({required this.id, required this.name, required this.cash, required this.buildingCityIds});

  final String id;
  final String name;
  final double cash;
  final List<String> buildingCityIds;

  factory ContextCompanyOption.fromJson(Map<String, dynamic> json) {
    final buildings = (json['buildings'] as List<dynamic>?) ?? const [];
    return ContextCompanyOption(
      id: json['id'] as String,
      name: (json['name'] as String?) ?? '',
      cash: (json['cash'] as num?)?.toDouble() ?? 0,
      buildingCityIds: buildings.map((b) => (b as Map<String, dynamic>)['cityId'] as String?).whereType<String>().toList(),
    );
  }
}

/// The authenticated player's current acting-account context, mirroring
/// `Player`'s lightweight `me` query fields (`Api/Types/Query.Auth.cs`'s
/// `GetMe`) — the same cheap, no-auth-heavy-computation query the web's
/// `stores/auth.ts` `fetchMe()`/`PLAYER_SELECTION` uses for this UI, rather
/// than the heavy `personAccount` query (portfolio/dividends/trades).
class ActiveAccountInfo {
  const ActiveAccountInfo({
    required this.playerId,
    required this.displayName,
    required this.availableCash,
    required this.activeAccountType,
    required this.activeCompanyId,
  });

  final String playerId;
  final String displayName;
  final double availableCash;

  /// `PERSON` or `COMPANY`.
  final String activeAccountType;
  final String? activeCompanyId;

  factory ActiveAccountInfo.fromJson(Map<String, dynamic> json) => ActiveAccountInfo(
    playerId: json['id'] as String,
    displayName: (json['displayName'] as String?) ?? '',
    availableCash: (json['personalCash'] as num?)?.toDouble() ?? 0,
    activeAccountType: (json['activeAccountType'] as String?) ?? 'PERSON',
    activeCompanyId: json['activeCompanyId'] as String?,
  );
}

/// A city entry for the switcher's city list, mirroring `cities` (also
/// used by `GlobalExchangeService`/`CitiesService`).
class ContextCityOption {
  const ContextCityOption({required this.id, required this.name, required this.countryCode});

  final String id;
  final String name;
  final String countryCode;

  factory ContextCityOption.fromJson(Map<String, dynamic> json) => ContextCityOption(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    countryCode: (json['countryCode'] as String?) ?? '',
  );
}
