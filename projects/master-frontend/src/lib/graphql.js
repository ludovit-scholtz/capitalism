const GRAPHQL_URL = import.meta.env.VITE_GRAPHQL_URL || 'https://localhost:44364/graphql';
export class GraphQLError extends Error {
    constructor(message) {
        super(message);
        this.name = 'GraphQLError';
    }
}
export async function gqlRequest(query, variables, token) {
    const headers = {
        'Content-Type': 'application/json',
    };
    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }
    const res = await fetch(GRAPHQL_URL, {
        method: 'POST',
        headers,
        body: JSON.stringify({ query, variables }),
    });
    const json = await res.json();
    if (json.errors?.length) {
        throw new GraphQLError(json.errors[0]?.message || 'Master API error');
    }
    if (!json.data) {
        throw new GraphQLError('No data returned from GraphQL API');
    }
    return json.data;
}
