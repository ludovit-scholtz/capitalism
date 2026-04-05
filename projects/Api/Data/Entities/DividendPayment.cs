using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Records an annual dividend payout from a company to either a player's personal account or another company.
/// </summary>
public sealed class DividendPayment
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid? RecipientPlayerId { get; set; }
    public Player? RecipientPlayer { get; set; }

    public Guid? RecipientCompanyId { get; set; }
    public Company? RecipientCompany { get; set; }

    public decimal ShareCount { get; set; }

    public decimal AmountPerShare { get; set; }

    public decimal TotalAmount { get; set; }

    public int GameYear { get; set; }

    public long RecordedAtTick { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}