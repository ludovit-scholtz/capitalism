import { resolveApiBaseUrl, resolveMasterGraphqlUrl } from '../runtimeGraphqlUrl'

/** Base URL of the master API for the few REST calls (GraphQL goes through `gqlRequest`). */
export const MASTER_API_BASE_URL = resolveApiBaseUrl(resolveMasterGraphqlUrl(import.meta.env.VITE_GRAPHQL_URL))
