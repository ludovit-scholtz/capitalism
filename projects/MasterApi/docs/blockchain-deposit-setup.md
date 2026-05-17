# Blockchain Deposit Setup (Algorand & VOI)

## Overview

The MasterApi includes a background service (`BlockchainDepositScannerHostedService`) that automatically
monitors the Algorand and VOI blockchains for incoming tokenized gold deposits. When a player submits
a gold token deposit request, the system generates a unique note text (`CAP-{requestId}`) that the
player must include in their on-chain transaction. The scanner reads transaction note fields, matches
them to pending requests, and automatically confirms deposits.

Both Algorand and VOI use the **Algorand Virtual Machine (AVM)** REST API — the same indexer URL
structure works for both networks.

---

## How It Works

### 1. Player Creates a Deposit Request

The player calls `createGoldTokenDepositRequest` (GraphQL mutation). The system returns:
- `depositAddress` — the address the player must send funds to
- `noteText` — the note the player **must include** in their on-chain transaction (format: `CAP-{uuid}`)

The player does **not** write this note manually — the system generates it automatically as
`CAP-{requestId}` so the scanner can match the transaction to the pending request.

### 2. Player Sends the On-Chain Transaction

The player sends an AVM asset transfer (`axfer`) transaction:
- **Recipient**: the `depositAddress` from the request
- **Asset ID**: the gold asset ID for the chosen network
- **Amount**: the number of gold units (multiply gram amount by `100,000,000` for 8 decimals)
- **Note field**: the `noteText` value from the request (e.g., `CAP-550e8400-e29b-41d4-a716-446655440000`)

### 3. Scanner Detects and Confirms the Deposit

The background scanner polls each network every `ScanIntervalSeconds` (default: 10s). For each
scan it:
1. Queries the AVM indexer for transactions with note prefix `CAP-` (base64: `Q0FQLQ==`)
2. Finds matching pending `GoldTokenDepositRequest` rows by parsing `CAP-{guid}` from the note
3. Verifies the received amount is ≥ the requested amount
4. Credits `player.GoldTokenBalance` and sets `request.Status = "CONFIRMED"`
5. Creates a `GoldTokenTransaction` audit record

---

## Configuration

The following keys live under the `GoldTokenTransfers` section in `appsettings.json`:

| Key | Description | Default |
|-----|-------------|---------|
| `AlgorandDepositAddress` | Deposit wallet address on Algorand | *(required)* |
| `VoiDepositAddress` | Deposit wallet address on VOI | *(required)* |
| `AlgorandIndexerUrl` | Algorand indexer base URL | `https://mainnet-idx.algonode.cloud` |
| `VoiIndexerUrl` | VOI indexer base URL | `https://mainnet-idx.voi.nodly.io` |
| `ScanIntervalSeconds` | Seconds between scan cycles | `10` |

### Stage Configuration (`appsettings.Development.json`)

```json
"GoldTokenTransfers": {
  "AlgorandDepositAddress": "7JXZD6DGSOZRSXKRCJ7UA73NHEHLBUCL2JK52RL3GTU5UF6SWQDBXHVHHQ",
  "VoiDepositAddress": "7JXZD6DGSOZRSXKRCJ7UA73NHEHLBUCL2JK52RL3GTU5UF6SWQDBXHVHHQ",
  "AlgorandIndexerUrl": "https://mainnet-idx.algonode.cloud",
  "VoiIndexerUrl": "https://mainnet-idx.voi.nodly.io",
  "ScanIntervalSeconds": 10
}
```

### Production Configuration (`appsettings.json`)

```json
"GoldTokenTransfers": {
  "AlgorandDepositAddress": "QFR2FSX4SMIQ2DXIZQTDNKRN4RDDCD7UTEI425LICP5ST5NGYDJEV6ZSSE",
  "VoiDepositAddress": "QFR2FSX4SMIQ2DXIZQTDNKRN4RDDCD7UTEI425LICP5ST5NGYDJEV6ZSSE",
  "AlgorandIndexerUrl": "https://mainnet-idx.algonode.cloud",
  "VoiIndexerUrl": "https://mainnet-idx.voi.nodly.io",
  "ScanIntervalSeconds": 10
}
```

> **Note**: The same wallet address is used for both Algorand and VOI networks. The networks are
> separate blockchains — a transaction on Algorand does not appear on VOI and vice versa. The
> scanner handles them independently using the correct network-specific asset IDs.

---

## Asset IDs

| Network | Asset Name | Asset ID |
|---------|-----------|----------|
| Algorand | Tokenized Gold | `1241944285` |
| VOI | Tokenized Gold | `302228` |

---

## Deposit Address Setup

### Requirements

You need a funded AVM wallet that:
1. Has opted in to the gold asset on both networks (you must hold at least 1 unit to opt in)
2. Can receive asset transfers from any sender

### Stage Address

```
7JXZD6DGSOZRSXKRCJ7UA73NHEHLBUCL2JK52RL3GTU5UF6SWQDBXHVHHQ
```

This address receives test deposits on both Algorand mainnet and VOI mainnet for the staging
environment.

### Production Address

```
QFR2FSX4SMIQ2DXIZQTDNKRN4RDDCD7UTEI425LICP5ST5NGYDJEV6ZSSE
```

This address receives real deposits on both Algorand mainnet and VOI mainnet for the production
environment.

### Opt-In to Assets

Before the deposit address can receive tokenized gold, it must opt in to each asset ID:

**Algorand** (asset ID `1241944285`):
```bash
goal asset optin --assetid 1241944285 --account QFR2FSX4SMIQ2DXIZQTDNKRN4RDDCD7UTEI425LICP5ST5NGYDJEV6ZSSE
```

**VOI** (asset ID `302228`):
```bash
goal asset optin --assetid 302228 --account QFR2FSX4SMIQ2DXIZQTDNKRN4RDDCD7UTEI425LICP5ST5NGYDJEV6ZSSE
```

You can also use any AVM-compatible wallet (Pera Wallet, Defly, etc.) to opt in.

---

## Scanner Technical Details

### Indexer Query

The scanner polls the indexer with:
```
GET {indexerUrl}/v2/accounts/{depositAddress}/transactions
  ?note-prefix=Q0FQLQ==   (base64 of "CAP-")
  &asset-id={assetId}
  &min-round={lastScannedRound}
  &limit=50
  &tx-type=axfer
```

### Note Field Format

- Player sends the note `CAP-{requestId}` in the transaction (AVM note field, UTF-8)
- The AVM node base64-encodes all note fields
- The indexer returns them base64-encoded; the scanner decodes them back to UTF-8
- The prefix filter `Q0FQLQ==` (base64 of `CAP-`) reduces unnecessary network traffic

### Gold Amount Precision

AVM amounts are integers in the smallest unit. The tokenized gold asset uses **8 decimal places**:

```
displayed_amount = raw_amount / 100_000_000
```

Example: `raw_amount = 500000000` → `5.00000000` grams

### Minimum Amount Check

If the on-chain amount received is **less than** the requested amount, the deposit is skipped with
a warning log. The deposit request remains `PENDING` and can be manually reviewed or the player
can retry.

### Status Lifecycle

| Status | Meaning |
|--------|---------|
| `PENDING` | Request created, waiting for on-chain transaction |
| `CONFIRMED` | Automatically matched and confirmed by the scanner |
| `REJECTED` | Manually rejected by an admin |

### Audit Trail

On automatic confirmation, the scanner creates a `GoldTokenTransaction` with:
- `AdminEmail = "system@capitalism.blockchain"`
- `Note = "DEPOSIT_AUTO_CONFIRMED:{network}:{requestId}:txid:{txId}"`
- `AdminNote = "AUTO:network={network}:txid={txId}:received={amount}"`

---

## Security Notes

1. **Do not share private keys** for the deposit addresses. The scanner only reads from the
   indexer — it does not need signing keys.
2. The scanner queries are read-only. Only `GoldTokenDepositRequest` status and
   `PlayerAccount.GoldTokenBalance` are updated.
3. Each transaction ID (`txId`) is processed only once. If the same transaction is returned in
   multiple scan cycles (within the same `min-round` window), the matching deposit will already
   be `CONFIRMED` and skipped.
4. Configure the deposit address as "not-configured" (the default placeholder) to disable
   automatic scanning for a network. The scanner skips empty or placeholder addresses.

---

## Environment Variable Overrides (Docker / Kubernetes)

The `GoldTokenTransfers` section can be overridden via environment variables using the standard
.NET configuration naming convention (double underscore as path separator):

```env
GoldTokenTransfers__AlgorandDepositAddress=QFR2FSX4SMIQ2DXIZQTDNKRN4RDDCD7UTEI425LICP5ST5NGYDJEV6ZSSE
GoldTokenTransfers__VoiDepositAddress=QFR2FSX4SMIQ2DXIZQTDNKRN4RDDCD7UTEI425LICP5ST5NGYDJEV6ZSSE
GoldTokenTransfers__AlgorandIndexerUrl=https://mainnet-idx.algonode.cloud
GoldTokenTransfers__VoiIndexerUrl=https://mainnet-idx.voi.nodly.io
GoldTokenTransfers__ScanIntervalSeconds=10
```
