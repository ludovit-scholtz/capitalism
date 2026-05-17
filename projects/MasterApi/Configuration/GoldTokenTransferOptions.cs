namespace MasterApi.Configuration;

public sealed class GoldTokenTransferOptions
{
    public const string SectionName = "GoldTokenTransfers";

    public string VoiDepositAddress { get; init; } = "voi-deposit-address-not-configured";

    public string AlgorandDepositAddress { get; init; } = "algorand-deposit-address-not-configured";
}
