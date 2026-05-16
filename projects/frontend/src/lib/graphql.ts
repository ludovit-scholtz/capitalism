import { resolveGameGraphqlUrl } from './runtimeGraphqlUrl'

const GRAPHQL_URL = resolveGameGraphqlUrl(import.meta.env.VITE_GRAPHQL_URL)

export interface GraphQLResponse<T> {
  data?: T
  errors?: Array<{ message: string; extensions?: Record<string, unknown> }>
}

/**
 * Structured error thrown by gqlRequest when the API returns a GraphQL error.
 * Carries the machine-readable `code` from `extensions.code` (e.g. `LOT_ALREADY_OWNED`)
 * so callers can distinguish specific failure types from generic API errors.
 */
export class GraphQLError extends Error {
  constructor(
    message: string,
    public readonly code?: string,
  ) {
    super(message)
    this.name = 'GraphQLError'
  }
}

/**
 * Lightweight GraphQL client that sends requests to the Events API.
 * Uses cookie-based browser sessions with credentials included.
 * Throws `GraphQLError` (with `.code` from `extensions.code`) on API errors.
 */
export async function gqlRequest<T>(query: string, variables?: Record<string, unknown>): Promise<T> {
  const res = await fetch(GRAPHQL_URL, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ query, variables }),
  })

  const json: GraphQLResponse<T> = await res.json()

  if (json.errors && json.errors.length > 0) {
    const firstError = json.errors[0]!
    const code = firstError.extensions?.code as string | undefined
    if (code === 'FORBIDDEN' || code === 'NOT_FOUND_OR_NOT_OWNED' || code === 'NOT_OWNED_OR_NOT_FOUND') {
      throw new GraphQLError("You don't have permission to perform this action.", code)
    }
    throw new GraphQLError(firstError.message, code)
  }

  if (!json.data) {
    throw new GraphQLError('No data returned from GraphQL API')
  }

  return json.data
}
