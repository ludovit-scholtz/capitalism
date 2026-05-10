namespace Api.Types;

/// <summary>Result of a forex quote request — preview of what a swap will cost.</summary>
public sealed class ForexQuoteResult
{
    /// <summary>Currency being sold (source).</summary>
    public string FromCurrencyCode { get; set; } = string.Empty;

    /// <summary>Currency being bought (target).</summary>
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Amount in the source currency that the player wants to sell.</summary>
    public decimal FromAmount { get; set; }

    /// <summary>
    /// Amount the player will receive in the target currency after the 1% fee is deducted.
    /// Formula: grossToAmount * (1 - FeePercent/100).
    /// </summary>
    public decimal ToAmount { get; set; }

    /// <summary>Fee charged on this swap, denominated in the source currency.</summary>
    public decimal FeeAmount { get; set; }

    /// <summary>Fee percentage applied (1.0 = 1%).</summary>
    public decimal FeePercent { get; set; } = 1m;

    /// <summary>
    /// How many units of ToCurrency equal 1 unit of FromCurrency.
    /// Example: 25.20 when From=EUR, To=CZK means 1 EUR ≈ 25.20 CZK.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>The player's current balance in the source currency.</summary>
    public decimal AvailableFromBalance { get; set; }

    /// <summary>Symbol for the source currency (e.g. "€", "$").</summary>
    public string FromCurrencySymbol => Mutation.GetCurrencySymbol(FromCurrencyCode);

    /// <summary>Symbol for the target currency (e.g. "Kč", "£").</summary>
    public string ToCurrencySymbol => Mutation.GetCurrencySymbol(ToCurrencyCode);

    /// <summary>
    /// Single-use UUID v4 nonce identifying this quote. Must be passed back in
    /// <c>ExecuteForexSwapInput.QuoteNonce</c> to prove the quote is fresh and unmodified.
    /// </summary>
    public Guid QuoteNonce { get; set; }

    /// <summary>UTC timestamp when this quote was issued by the server.</summary>
    public DateTime QuotedAtUtc { get; set; }

    /// <summary>Number of seconds the quote remains valid (default 30).</summary>
    public int QuoteExpiresInSeconds { get; set; } = 30;
}

/// <summary>Result of a successfully executed forex swap.</summary>
public sealed class ForexTradeResult
{
    /// <summary>ID of the recorded trade.</summary>
    public Guid TradeId { get; set; }

    /// <summary>Currency sold.</summary>
    public string FromCurrencyCode { get; set; } = string.Empty;

    /// <summary>Currency bought.</summary>
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Amount sold in the source currency.</summary>
    public decimal FromAmount { get; set; }

    /// <summary>Amount received in the target currency.</summary>
    public decimal ToAmount { get; set; }

    /// <summary>Fee charged for the swap (in source currency).</summary>
    public decimal FeeAmount { get; set; }

    /// <summary>Exchange rate used for this trade.</summary>
    public decimal Rate { get; set; }

    /// <summary>Remaining balance in the source currency after the swap.</summary>
    public decimal NewFromBalance { get; set; }

    /// <summary>New balance in the target currency after the swap.</summary>
    public decimal NewToBalance { get; set; }

    /// <summary>Symbol for the source currency.</summary>
    public string FromCurrencySymbol => Mutation.GetCurrencySymbol(FromCurrencyCode);

    /// <summary>Symbol for the target currency.</summary>
    public string ToCurrencySymbol => Mutation.GetCurrencySymbol(ToCurrencyCode);
}

/// <summary>A single currency balance held in a player's personal multi-currency wallet.</summary>
public sealed class CurrencyBalanceResult
{
    /// <summary>ISO 4217 currency code.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Display symbol for the currency.</summary>
    public string CurrencySymbol => Mutation.GetCurrencySymbol(CurrencyCode);

    /// <summary>Current balance in this currency.</summary>
    public decimal Balance { get; set; }
}

/// <summary>A single recorded forex trade shown in the player's trade history.</summary>
public sealed class ForexTradeHistoryEntry
{
    /// <summary>Trade ID.</summary>
    public Guid Id { get; set; }

    /// <summary>Currency sold.</summary>
    public string FromCurrencyCode { get; set; } = string.Empty;

    /// <summary>Currency bought.</summary>
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Amount sold.</summary>
    public decimal FromAmount { get; set; }

    /// <summary>Amount received.</summary>
    public decimal ToAmount { get; set; }

    /// <summary>Fee charged.</summary>
    public decimal FeeAmount { get; set; }

    /// <summary>Rate used.</summary>
    public decimal Rate { get; set; }

    /// <summary>Game tick when the trade was executed.</summary>
    public long ExecutedAtTick { get; set; }

    /// <summary>UTC timestamp when the trade was executed.</summary>
    public DateTime ExecutedAtUtc { get; set; }

    /// <summary>Symbol for the source currency.</summary>
    public string FromCurrencySymbol => Mutation.GetCurrencySymbol(FromCurrencyCode);

    /// <summary>Symbol for the target currency.</summary>
    public string ToCurrencySymbol => Mutation.GetCurrencySymbol(ToCurrencyCode);
}

/// <summary>
/// A single EUR-based FX rate entry used for frontend price scaling.
/// Rate means "units of CurrencyCode per 1 EUR".
/// </summary>
public sealed class EurFxRate
{
    private const decimal SpreadHalfPercent = 0.005m;

    /// <summary>ISO 4217 currency code of the quote currency.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>How many units of this currency equal 1 EUR (e.g. 25.20 for CZK, 1.08 for USD).</summary>
    public decimal Rate { get; set; }

    /// <summary>Mid-market rate (same as Rate for backwards compatibility).</summary>
    public decimal MidRate => Rate;

    /// <summary>Buy rate (ask): slightly worse for buyer, +0.5% from mid.</summary>
    public decimal BuyRate => Math.Round(Rate * (1m + SpreadHalfPercent), 6);

    /// <summary>Sell rate (bid): slightly better for seller, -0.5% from mid.</summary>
    public decimal SellRate => Math.Round(Rate * (1m - SpreadHalfPercent), 6);
}
