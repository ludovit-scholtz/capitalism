const GRAPHQL_URL = import.meta.env.VITE_GRAPHQL_URL || 'http://localhost:44364/graphql'
const COOKIE_SESSION_SENTINEL = 'cookie-session'

export interface GraphQLResponse<T> {
  data?: T
  errors?: Array<{ message: string; extensions?: Record<string, unknown> }>
}

export class GraphQLError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'GraphQLError'
  }
}

export async function gqlRequest<T>(
  query: string,
  variables?: Record<string, unknown>,
  token?: string | null,
  overrideUrl?: string,
): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  }

  if (token && token !== COOKIE_SESSION_SENTINEL) {
    headers['Authorization'] = `Bearer ${token}`
  }

  const res = await fetch(overrideUrl ?? GRAPHQL_URL, {
    method: 'POST',
    credentials: 'include',
    headers,
    body: JSON.stringify({ query, variables }),
  })

  const json: GraphQLResponse<T> = await res.json()

  if (json.errors?.length) {
    throw new GraphQLError(json.errors[0]?.message || 'Master API error')
  }

  if (!json.data) {
    throw new GraphQLError('No data returned from GraphQL API')
  }

  return json.data
}
