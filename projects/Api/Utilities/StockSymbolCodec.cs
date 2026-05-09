namespace Api.Utilities;

public static class StockSymbolCodec
{
    public static string FromCompanyId(Guid companyId)
        => $"CMP-{companyId:N}".ToUpperInvariant();

    public static bool TryParseCompanyId(string? stockSymbol, out Guid companyId)
    {
        companyId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(stockSymbol))
        {
            return false;
        }

        var normalized = stockSymbol.Trim();
        if (!normalized.StartsWith("CMP-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParseExact(normalized[4..], "N", out companyId);
    }
}
