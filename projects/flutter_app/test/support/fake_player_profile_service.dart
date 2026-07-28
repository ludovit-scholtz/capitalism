import 'package:capitalism_app/features/leaderboard/leaderboard_models.dart';
import 'package:capitalism_app/features/leaderboard/player_profile_service.dart';

class FakePlayerProfileService implements PlayerProfileService {
  FakePlayerProfileService({
    this.myPlayerId,
    this.sessions = const [],
    this.sessionsError,
    this.logoutAllError,
    this.bioError,
    this.displayNameError,
  });

  final String? myPlayerId;
  final List<PlayerSession> sessions;
  final Object? sessionsError;
  final Object? logoutAllError;
  final Object? bioError;
  final Object? displayNameError;

  final List<String> calls = [];
  String? lastSavedBio;
  String? lastSavedDisplayName;
  bool loggedOutAll = false;

  @override
  Future<String?> fetchMyPlayerId() async {
    calls.add('fetchMyPlayerId');
    return myPlayerId;
  }

  @override
  Future<String?> updateBio(String? bio) async {
    calls.add('updateBio');
    if (bioError != null) throw bioError!;
    lastSavedBio = bio;
    return bio;
  }

  @override
  Future<String> updateDisplayName(String displayName) async {
    calls.add('updateDisplayName');
    if (displayNameError != null) throw displayNameError!;
    lastSavedDisplayName = displayName;
    return displayName;
  }

  @override
  Future<List<PlayerSession>> fetchSessions() async {
    calls.add('fetchSessions');
    if (sessionsError != null) throw sessionsError!;
    return sessions;
  }

  @override
  Future<void> logoutAllDevices() async {
    calls.add('logoutAllDevices');
    if (logoutAllError != null) throw logoutAllError!;
    loggedOutAll = true;
  }
}
