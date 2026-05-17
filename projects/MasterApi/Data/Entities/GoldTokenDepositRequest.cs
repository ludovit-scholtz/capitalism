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

    /// <summary>
    /// The note text the depositor must include in the blockchain transaction note field.
    /// Format: "CAP-{Id}" — used by the automated scanner to match incoming transactions.
    /// </summary>
    public string NoteText { get; set; } = string.Empty;

    public PlayerAccount PlayerAccount { get; set; } = null!;
}
