using System.ComponentModel.DataAnnotations;
using Api.Data.Entities;

namespace Api.Types;

/// <summary>Input for purchasing company shares from the public stock exchange.</summary>
public sealed class BuySharesInput
{
    public Guid CompanyId { get; set; }

    public decimal ShareCount { get; set; }

    /// <summary>Optional: override the server-side active account. Accepted values are "PERSON" or "COMPANY".</summary>
    [MaxLength(10)]
    public string? TradeAccountType { get; set; }

    /// <summary>Optional: company ID to trade from when TradeAccountType is "COMPANY".</summary>
    public Guid? TradeAccountCompanyId { get; set; }

    /// <summary>
    /// Required settlement account for this trade. Must belong to the active trade account
    /// (person/company) and be denominated in USD.
    /// </summary>
    public Guid? BankAccountId { get; set; }
}

/// <summary>Input for selling company shares back to the public stock exchange.</summary>
public sealed class SellSharesInput
{
    public Guid CompanyId { get; set; }

    public decimal ShareCount { get; set; }

    /// <summary>Optional: override the server-side active account. Accepted values are "PERSON" or "COMPANY".</summary>
    [MaxLength(10)]
    public string? TradeAccountType { get; set; }

    /// <summary>Optional: company ID to trade from when TradeAccountType is "COMPANY".</summary>
    public Guid? TradeAccountCompanyId { get; set; }

    /// <summary>
    /// Required settlement account for this trade. Must belong to the active trade account
    /// (person/company) and be denominated in USD.
    /// </summary>
    public Guid? BankAccountId { get; set; }
}
