// Own-profile write operations for the Player Profile screen: bio/display-
// name editing and session security (list active sessions, log out other
// devices), ported from `projects/frontend/src/views/PlayerProfileView.vue`.
// Trimmed: no `GenderPicker`/regenerate-random-name flow — plain text
// editing only, `gender` omitted from both mutations (optional server-side).

import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../core/auth/auth_state.dart';
import '../../core/config/app_config.dart';
import '../../core/graphql/graphql_service.dart';
import 'leaderboard_models.dart';

const _myPlayerIdQuery = r'''
  query MyPlayerId { me { id } }
''';

const _updatePlayerBioMutation = r'''
  mutation UpdatePlayerBio($input: UpdatePlayerBioInput!) {
    updatePlayerBio(input: $input) { playerId bio }
  }
''';

const _updateDisplayNameMutation = r'''
  mutation UpdateDisplayName($input: UpdateDisplayNameInput!) {
    updateDisplayName(input: $input) { playerId displayName gender }
  }
''';

const _updatePersonalAccountNameMasterMutation = r'''
  mutation UpdatePersonalAccountName($input: UpdatePersonalAccountNameInput!) {
    updatePersonalAccountName(input: $input) { playerId personalAccountName gender }
  }
''';

class PlayerProfileService {
  PlayerProfileService(this._graphQlService, this._authState, {http.Client? client}) : _client = client ?? http.Client();

  final GraphQlService _graphQlService;
  final AuthState _authState;
  final http.Client _client;

  Future<String?> fetchMyPlayerId() async {
    try {
      final result = await _graphQlService.request(_myPlayerIdQuery);
      return (result['me'] as Map<String, dynamic>?)?['id'] as String?;
    } catch (_) {
      return null;
    }
  }

  Future<String?> updateBio(String? bio) async {
    final result = await _graphQlService.request(
      _updatePlayerBioMutation,
      variables: {
        'input': {'bio': bio},
      },
    );
    return (result['updatePlayerBio'] as Map<String, dynamic>?)?['bio'] as String?;
  }

  /// Updates the display name on both the game API (used in-game — chat,
  /// building pages, rankings) and the Master API's `personalAccountName`
  /// (the alias shown on the cross-server leaderboard) so the two stay in
  /// sync, matching the web's dual-write.
  Future<String> updateDisplayName(String displayName) async {
    await _graphQlService.request(
      _updatePersonalAccountNameMasterMutation,
      variables: {
        'input': {'personalAccountName': displayName},
      },
      endpoint: AppConfig.masterGraphqlUrl,
    );
    final result = await _graphQlService.request(
      _updateDisplayNameMutation,
      variables: {
        'input': {'displayName': displayName},
      },
    );
    return ((result['updateDisplayName'] as Map<String, dynamic>?)?['displayName'] as String?) ?? displayName;
  }

  Future<List<PlayerSession>> fetchSessions() async {
    final response = await _client.get(
      Uri.parse('${AppConfig.gameApiBaseUrl}/auth/sessions'),
      headers: _authHeaders,
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('Could not load active sessions.');
    }
    final decoded = jsonDecode(response.body) as Map<String, dynamic>;
    final list = decoded['sessions'] as List<dynamic>? ?? const [];
    return list.map((e) => PlayerSession.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> logoutAllDevices() async {
    final response = await _client.post(
      Uri.parse('${AppConfig.gameApiBaseUrl}/auth/logout-all'),
      headers: _authHeaders,
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('Could not log out other devices.');
    }
  }

  Map<String, String> get _authHeaders {
    final token = _authState.token;
    return {if (token != null && token.isNotEmpty) 'Authorization': 'Bearer $token'};
  }
}
