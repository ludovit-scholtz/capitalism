namespace MasterApi.Configuration;

public sealed class GoldTokenTransferOptions
{
    public const string SectionName = "GoldTokenTransfers";

    public string VoiDepositAddress { get; init; } = "voi-deposit-address-not-configured";

    /// <summary>Algorand indexer API URL for scanning incoming transactions.</summary>
    public string AlgorandIndexerUrl { get; init; } = "https://mainnet-idx.algonode.cloud";

    /// <summary>VOI indexer API URL for scanning incoming transactions.</summary>
    public string VoiIndexerUrl { get; init; } = "https://mainnet-idx.voi.nodly.io";

    /// <summary>How often to poll each blockchain for new deposit transactions (in seconds).</summary>
    public int ScanIntervalSeconds { get; init; } = 10;

    public string AlgorandDepositAddress { get; init; } = "algorand-deposit-address-not-configured";
}
