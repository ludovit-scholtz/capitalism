/// Mirrors `RankingRewardHistoryItem` in
/// `projects/MasterApi/Types/Inputs.Ranking.cs` — one row of a player's
/// awarded (`status == "AWARDED"`) master-ranking bounty history.
class CompletedBounty {
  const CompletedBounty({
    required this.id,
    required this.bountyCode,
    required this.bountyDisplayName,
    required this.pointsAwarded,
    required this.status,
    required this.serverKey,
    required this.eventDateUtc,
    required this.awardedAtUtc,
  });

  factory CompletedBounty.fromJson(Map<String, dynamic> json) {
    return CompletedBounty(
      id: json['id'] as String,
      bountyCode: json['bountyCode'] as String,
      bountyDisplayName: json['bountyDisplayName'] as String,
      pointsAwarded: (json['pointsAwarded'] as num?)?.toDouble() ?? 0,
      status: json['status'] as String? ?? '',
      serverKey: json['serverKey'] as String?,
      eventDateUtc: json['eventDateUtc'] as String? ?? '',
      awardedAtUtc: json['awardedAtUtc'] as String? ?? '',
    );
  }

  final String id;
  final String bountyCode;
  final String bountyDisplayName;
  final double pointsAwarded;
  final String status;
  final String? serverKey;
  final String eventDateUtc;
  final String awardedAtUtc;
}
