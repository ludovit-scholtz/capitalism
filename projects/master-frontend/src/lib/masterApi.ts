import { gqlRequest } from './graphql'

export interface GameServerSummary {
  id: string
  serverKey: string
  displayName: string
  description: string
  region: string
  environment: string
  backendUrl: string
  graphqlUrl: string
  frontendUrl: string
  version: string
  playerCount: number
  companyCount: number
  currentTick: number
  registeredAtUtc: string
  lastHeartbeatAtUtc: string
  isOnline: boolean
}

interface GameServersPayload {
  gameServers: GameServerSummary[]
}

const GAME_SERVERS_QUERY = `
  query GetGameServers {
    gameServers {
      id
      serverKey
      displayName
      description
      region
      environment
      backendUrl
      graphqlUrl
      frontendUrl
      version
      playerCount
      companyCount
      currentTick
      registeredAtUtc
      lastHeartbeatAtUtc
      isOnline
    }
  }
`

export async function fetchGameServers(): Promise<GameServerSummary[]> {
  const data = await gqlRequest<GameServersPayload>(GAME_SERVERS_QUERY)
  return data.gameServers
}