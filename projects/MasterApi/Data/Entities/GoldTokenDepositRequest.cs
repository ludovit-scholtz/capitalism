namespace MasterApi.Data.Entities;

public sealed class GoldTokenDepositRequest
{
    public Guid Id { get; set; }

    public Guid PlayerAccountId { get; set; }

    public string PlayerEmail { get; set; } = string.Empty;

    public string Network { get; set; } = "ALGORAND";

    public long AssetId { get; set; }

    public string DepositAddress { get; set; } = string.Empty;

    public string? SenderAddress { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = "PENDING";

    public DateTime RequestedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public string? ProcessedByEmail { get; set; }

    public string? AdminNote { get; set; }

    public PlayerAccount PlayerAccount { get; set; } = null!;
}
