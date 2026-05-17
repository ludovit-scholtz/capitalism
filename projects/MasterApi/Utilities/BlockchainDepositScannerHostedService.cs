using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MasterApi.Types;

namespace MasterApi.Utilities;

/// <summary>
/// Background service that periodically scans the Algorand and VOI blockchains for incoming
/// deposit transactions. When a transaction's note field matches a pending deposit request
/// (format: "CAP-{requestId}"), the request is automatically confirmed and the player's
/// gold token balance is credited.
/// </summary>
public sealed class BlockchainDepositScannerHostedService : BackgroundService
{
    private const string NotePrefix = "CAP-";
    private const string StatusPending = "PENDING";
    private const string StatusConfirmed = "CONFIRMED";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GoldTokenTransferOptions _options;
    private readonly ILogger<BlockchainDepositScannerHostedService> _logger;
    private readonly HttpClient _httpClient;

    // Track the last scanned round per network to avoid reprocessing old transactions.
    private long _lastScannedRoundAlgorand;
    private long _lastScannedRoundVoi;

    public BlockchainDepositScannerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<GoldTokenTransferOptions> options,
        ILogger<BlockchainDepositScannerHostedService> logger,
        HttpClient httpClient)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BlockchainDepositScannerHostedService started. Algorand={AlgorandAddress} VOI={VoiAddress} Interval={Interval}s",
            _options.AlgorandDepositAddress, _options.VoiDepositAddress, _options.ScanIntervalSeconds);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.ScanIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await ScanNetworkAsync("ALGORAND", _options.AlgorandDepositAddress, _options.AlgorandIndexerUrl, stoppingToken);
            await ScanNetworkAsync("VOI", _options.VoiDepositAddress, _options.VoiIndexerUrl, stoppingToken);

            await Task.Delay(interval, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _logger.LogInformation("BlockchainDepositScannerHostedService stopped.");
    }

    private async Task ScanNetworkAsync(
        string network,
        string depositAddress,
        string indexerBaseUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(depositAddress)
            || depositAddress.Contains("not-configured", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(indexerBaseUrl))
        {
            return;
        }

        try
        {
            var assetId = network == "VOI"
                ? Mutation.VoiTokenizedGoldAssetId
                : Mutation.AlgorandTokenizedGoldAssetId;

            // AVM note-prefix filter requires the prefix to be base64-encoded.
            var notePrefixB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(NotePrefix));

            var isVoi = network == "VOI";
            var minRound = (isVoi ? _lastScannedRoundVoi : _lastScannedRoundAlgorand);
            minRound = minRound > 0 ? minRound : 0;

            var url = $"{indexerBaseUrl.TrimEnd('/')}/v2/accounts/{depositAddress}/transactions"
                + $"?note-prefix={Uri.EscapeDataString(notePrefixB64)}"
                + $"&asset-id={assetId}"
                + (minRound > 0 ? $"&min-round={minRound}" : string.Empty)
                + "&limit=50"
                + "&tx-type=axfer";

            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Blockchain scanner [{Network}]: indexer returned {StatusCode}", network, response.StatusCode);
                return;
            }

            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(
                cancellationToken: cancellationToken);

            if (doc is null)
            {
                return;
            }

            var root = doc.RootElement;

            // Update last scanned round from response metadata.
            if (root.TryGetProperty("current-round", out var currentRoundEl)
                && currentRoundEl.TryGetInt64(out var currentRound))
            {
                if (isVoi && currentRound > _lastScannedRoundVoi)
                    _lastScannedRoundVoi = currentRound;
                else if (!isVoi && currentRound > _lastScannedRoundAlgorand)
                    _lastScannedRoundAlgorand = currentRound;
            }

            if (!root.TryGetProperty("transactions", out var txArrayEl)
                || txArrayEl.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var tx in txArrayEl.EnumerateArray())
            {
                await ProcessTransactionAsync(network, tx, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BlockchainDepositScannerHostedService [{Network}]: error during scan", network);
        }
    }

    private async Task ProcessTransactionAsync(
        string network,
        JsonElement tx,
        CancellationToken cancellationToken)
    {
        // Decode the transaction note from base64.
        if (!tx.TryGetProperty("note", out var noteEl) || noteEl.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var noteB64 = noteEl.GetString();
        if (string.IsNullOrEmpty(noteB64))
        {
            return;
        }

        string noteText;
        try
        {
            var noteBytes = Convert.FromBase64String(noteB64);
            noteText = Encoding.UTF8.GetString(noteBytes).Trim();
        }
        catch
        {
            return;
        }

        if (!noteText.StartsWith(NotePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var requestIdStr = noteText[NotePrefix.Length..];
        if (!Guid.TryParse(requestIdStr, out var requestId))
        {
            return;
        }

        // Extract transaction ID and transferred amount.
        var txId = tx.TryGetProperty("id", out var txIdEl) ? (txIdEl.GetString() ?? "unknown") : "unknown";

        decimal receivedAmount = 0m;
        if (tx.TryGetProperty("asset-transfer-transaction", out var axferEl))
        {
            if (axferEl.TryGetProperty("amount", out var amountEl) && amountEl.TryGetInt64(out var amountRaw))
            {
                // AVM asset amounts are in the smallest unit (microunits).
                // Gold token has 8 decimal places, so divide by 10^8.
                receivedAmount = amountRaw / 100_000_000m;
            }
        }

        if (receivedAmount <= 0m)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

        var request = await db.GoldTokenDepositRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.Network == network, cancellationToken);

        if (request is null)
        {
            _logger.LogWarning(
                "Blockchain scanner [{Network}]: transaction {TxId} references unknown deposit request {RequestId}",
                network, txId, requestId);
            return;
        }

        if (request.Status != StatusPending)
        {
            // Already processed; skip silently.
            return;
        }

        if (receivedAmount < request.Amount)
        {
            _logger.LogWarning(
                "Blockchain scanner [{Network}]: transaction {TxId} received {Received} but request {RequestId} requires {Required} — skipping",
                network, txId, receivedAmount, requestId, request.Amount);
            return;
        }

        var player = await db.PlayerAccounts
            .FirstOrDefaultAsync(p => p.Id == request.PlayerAccountId, cancellationToken);

        if (player is null)
        {
            _logger.LogError(
                "Blockchain scanner [{Network}]: deposit request {RequestId} references missing player {PlayerId}",
                network, requestId, request.PlayerAccountId);
            return;
        }

        var balanceBefore = player.GoldTokenBalance;
        player.GoldTokenBalance += request.Amount;
        player.ConcurrencyToken = Guid.NewGuid();

        request.Status = StatusConfirmed;
        request.ProcessedAtUtc = DateTime.UtcNow;
        request.ProcessedByEmail = "system@capitalism.blockchain";
        request.AdminNote = $"AUTO:network={network}:txid={txId}:received={receivedAmount:0.########}";

        db.GoldTokenTransactions.Add(new GoldTokenTransaction
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            PlayerEmail = player.Email,
            Amount = request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = player.GoldTokenBalance,
            AdminEmail = "system@capitalism.blockchain",
            Note = $"DEPOSIT_AUTO_CONFIRMED:{request.Network}:{request.Id}:txid={txId}",
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Blockchain scanner [{Network}]: auto-confirmed deposit {RequestId} for player {PlayerEmail} — {Amount:0.########} g (tx {TxId})",
            network, requestId, player.Email, request.Amount, txId);
    }
}
