import { gqlRequest } from '../graphql'

// ── Player gold account ────────────────────────────────────────────────────

export interface PlayerGoldTransactionInfo {
  id: string
  amount: number
  balanceBefore: number
  balanceAfter: number
  note: string | null
  createdAtUtc: string
}

export interface PlayerGoldAccountInfo {
  goldTokenBalance: number
  lastUpdatedAtUtc: string | null
  recentTransactions: PlayerGoldTransactionInfo[]
}

export interface GoldDepositRequestInfo {
  id: string
  playerAccountId: string
  playerEmail: string
  network: 'VOI' | 'ALGORAND'
  assetId: number
  depositAddress: string
  senderAddress: string | null
  amount: number
  status: 'PENDING' | 'PROCESSED'
  requestedAtUtc: string
  processedAtUtc: string | null
  processedByEmail: string | null
  adminNote: string | null
}

export interface GoldWithdrawalRequestInfo {
  id: string
  playerAccountId: string
  playerEmail: string
  network: 'VOI' | 'ALGORAND'
  assetId: number
  destinationAddress: string
  amount: number
  status: 'PENDING' | 'PROCESSED'
  requestedAtUtc: string
  processedAtUtc: string | null
  processedByEmail: string | null
  adminNote: string | null
}

const MY_GOLD_ACCOUNT_QUERY = `
  query GetMyGoldAccount {
    myGoldAccount {
      goldTokenBalance
      lastUpdatedAtUtc
      recentTransactions {
        id
        amount
        balanceBefore
        balanceAfter
        note
        createdAtUtc
      }
    }
  }
`

const MY_GOLD_DEPOSIT_REQUESTS_QUERY = `
  query MyGoldDepositRequests {
    myGoldDepositRequests {
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

const MY_GOLD_WITHDRAWAL_REQUESTS_QUERY = `
  query MyGoldWithdrawalRequests {
    myGoldWithdrawalRequests {
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

const CREATE_GOLD_DEPOSIT_REQUEST_MUTATION = `
  mutation CreateGoldDepositRequest($input: CreateGoldTokenDepositRequestInput!) {
    createGoldTokenDepositRequest(input: $input) {
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

const CREATE_GOLD_WITHDRAWAL_REQUEST_MUTATION = `
  mutation CreateGoldWithdrawalRequest($input: CreateGoldTokenWithdrawalRequestInput!) {
    createGoldTokenWithdrawalRequest(input: $input) {
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

export async function fetchMyGoldAccount(token: string): Promise<PlayerGoldAccountInfo> {
  const data = await gqlRequest<{ myGoldAccount: PlayerGoldAccountInfo }>(
    MY_GOLD_ACCOUNT_QUERY,
    undefined,
    token,
  )
  return data.myGoldAccount
}

export async function fetchMyGoldDepositRequests(token: string): Promise<GoldDepositRequestInfo[]> {
  const data = await gqlRequest<{ myGoldDepositRequests: GoldDepositRequestInfo[] }>(
    MY_GOLD_DEPOSIT_REQUESTS_QUERY,
    undefined,
    token,
  )
  return data.myGoldDepositRequests
}

export async function fetchMyGoldWithdrawalRequests(
  token: string,
): Promise<GoldWithdrawalRequestInfo[]> {
  const data = await gqlRequest<{ myGoldWithdrawalRequests: GoldWithdrawalRequestInfo[] }>(
    MY_GOLD_WITHDRAWAL_REQUESTS_QUERY,
    undefined,
    token,
  )
  return data.myGoldWithdrawalRequests
}

export async function createGoldDepositRequest(
  token: string,
  network: 'VOI' | 'ALGORAND',
  amount: number,
  senderAddress?: string,
): Promise<GoldDepositRequestInfo> {
  const data = await gqlRequest<{ createGoldTokenDepositRequest: GoldDepositRequestInfo }>(
    CREATE_GOLD_DEPOSIT_REQUEST_MUTATION,
    { input: { network, amount, senderAddress: senderAddress ?? null } },
    token,
  )
  return data.createGoldTokenDepositRequest
}

export async function createGoldWithdrawalRequest(
  token: string,
  network: 'VOI' | 'ALGORAND',
  amount: number,
  destinationAddress: string,
): Promise<GoldWithdrawalRequestInfo> {
  const data = await gqlRequest<{ createGoldTokenWithdrawalRequest: GoldWithdrawalRequestInfo }>(
    CREATE_GOLD_WITHDRAWAL_REQUEST_MUTATION,
    { input: { network, amount, destinationAddress } },
    token,
  )
  return data.createGoldTokenWithdrawalRequest
}
