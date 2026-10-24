import type { GoldDepositRequestInfo, GoldWithdrawalRequestInfo } from './gold'
import { MASTER_API_BASE_URL } from './shared'
import { gqlRequest } from '../graphql'

// ── Gold token administration ──────────────────────────────────────────────

export interface GoldTokenBalanceInfo {
  playerId: string
  email: string
  displayName: string
  goldTokenBalance: number
}

export interface GoldTokenTransactionInfo {
  id: string
  playerEmail: string
  amount: number
  balanceBefore: number
  balanceAfter: number
  adminEmail: string
  note: string | null
  createdAtUtc: string
}

const GOLD_TOKEN_BALANCES_QUERY = `
  query GetGoldTokenBalances {
    goldTokenBalances {
      playerId
      email
      displayName
      goldTokenBalance
    }
  }
`

const GOLD_TOKEN_TRANSACTIONS_QUERY = `
  query GetGoldTokenTransactions($targetEmail: String, $limit: Int) {
    goldTokenTransactions(targetEmail: $targetEmail, limit: $limit) {
      id
      playerEmail
      amount
      balanceBefore
      balanceAfter
      adminEmail
      note
      createdAtUtc
    }
  }
`

const ADJUST_GOLD_TOKEN_MUTATION = `
  mutation AdjustGoldToken($input: AdjustGoldTokenInput!) {
    adjustGoldTokenBalance(input: $input) {
      playerId
      email
      displayName
      goldTokenBalance
    }
  }
`

const GOLD_DEPOSIT_REQUESTS_QUERY = `
  query GoldTokenDepositRequests {
    goldTokenDepositRequests {
      id
      playerAccountId
      playerEmail
      network
      assetId
      depositAddress
      senderAddress
      amount
      status
      requestedAtUtc
      processedAtUtc
      processedByEmail
      adminNote
    }
  }
`

const GOLD_WITHDRAWAL_REQUESTS_QUERY = `
  query GoldTokenWithdrawalRequests {
    goldTokenWithdrawalRequests {
      id
      playerAccountId
      playerEmail
      network
      assetId
      destinationAddress
      amount
      status
      requestedAtUtc
      processedAtUtc
      processedByEmail
      adminNote
    }
  }
`

const PROCESS_GOLD_DEPOSIT_REQUEST_MUTATION = `
  mutation ProcessGoldDepositRequest($input: ProcessGoldTokenDepositRequestInput!) {
    processGoldTokenDepositRequest(input: $input) {
      id
      status
      processedAtUtc
      processedByEmail
      adminNote
    }
  }
`

const PROCESS_GOLD_WITHDRAWAL_REQUEST_MUTATION = `
  mutation ProcessGoldWithdrawalRequest($input: ProcessGoldTokenWithdrawalRequestInput!) {
    processGoldTokenWithdrawalRequest(input: $input) {
      id
      status
      processedAtUtc
      processedByEmail
      adminNote
    }
  }
`

export async function fetchGoldTokenBalances(token: string): Promise<GoldTokenBalanceInfo[]> {
  const data = await gqlRequest<{ goldTokenBalances: GoldTokenBalanceInfo[] }>(
    GOLD_TOKEN_BALANCES_QUERY,
    undefined,
    token,
  )
  return data.goldTokenBalances
}

export async function fetchGoldTokenTransactions(
  token: string,
  targetEmail?: string,
  limit = 50,
): Promise<GoldTokenTransactionInfo[]> {
  const data = await gqlRequest<{ goldTokenTransactions: GoldTokenTransactionInfo[] }>(
    GOLD_TOKEN_TRANSACTIONS_QUERY,
    { targetEmail: targetEmail ?? null, limit },
    token,
  )
  return data.goldTokenTransactions
}

export async function adjustGoldTokenBalance(
  token: string,
  targetEmail: string,
  amount: number,
  note: string,
): Promise<GoldTokenBalanceInfo> {
  const data = await gqlRequest<{ adjustGoldTokenBalance: GoldTokenBalanceInfo }>(
    ADJUST_GOLD_TOKEN_MUTATION,
    { input: { targetEmail, amount, note } },
    token,
  )
  return data.adjustGoldTokenBalance
}

export async function fetchGoldDepositRequests(token: string): Promise<GoldDepositRequestInfo[]> {
  const data = await gqlRequest<{ goldTokenDepositRequests: GoldDepositRequestInfo[] }>(
    GOLD_DEPOSIT_REQUESTS_QUERY,
    undefined,
    token,
  )
  return data.goldTokenDepositRequests
}

export async function fetchGoldWithdrawalRequests(
  token: string,
): Promise<GoldWithdrawalRequestInfo[]> {
  const data = await gqlRequest<{ goldTokenWithdrawalRequests: GoldWithdrawalRequestInfo[] }>(
    GOLD_WITHDRAWAL_REQUESTS_QUERY,
    undefined,
    token,
  )
  return data.goldTokenWithdrawalRequests
}

export async function processGoldDepositRequest(
  token: string,
  requestId: string,
  adminNote?: string,
): Promise<GoldDepositRequestInfo> {
  const data = await gqlRequest<{ processGoldTokenDepositRequest: GoldDepositRequestInfo }>(
    PROCESS_GOLD_DEPOSIT_REQUEST_MUTATION,
    { input: { requestId, adminNote: adminNote ?? null } },
    token,
  )
  return data.processGoldTokenDepositRequest
}

export async function processGoldWithdrawalRequest(
  token: string,
  requestId: string,
  adminNote?: string,
): Promise<GoldWithdrawalRequestInfo> {
  const data = await gqlRequest<{ processGoldTokenWithdrawalRequest: GoldWithdrawalRequestInfo }>(
    PROCESS_GOLD_WITHDRAWAL_REQUEST_MUTATION,
    { input: { requestId, adminNote: adminNote ?? null } },
    token,
  )
  return data.processGoldTokenWithdrawalRequest
}

export async function revokePlayerSessions(token: string, playerId: string): Promise<void> {
  const response = await fetch(`${MASTER_API_BASE_URL}/admin/sessions/${playerId}/revoke-all`, {
    method: 'POST',
    credentials: 'include',
    headers:
      token && token !== 'cookie-session'
        ? {
            Authorization: `Bearer ${token}`,
          }
        : undefined,
  })
  if (!response.ok) {
    throw new Error('Failed to revoke sessions.')
  }
}
