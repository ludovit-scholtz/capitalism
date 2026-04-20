import { gqlRequest } from './graphql';
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
`;
const REGISTER_MUTATION = `
  mutation Register($input: RegisterInput!) {
    register(input: $input) {
      token
      expiresAtUtc
      player {
        id
        email
        displayName
        createdAtUtc
        startupPackClaimedAtUtc
        canClaimStartupPack
      }
    }
  }
`;
const LOGIN_MUTATION = `
  mutation Login($input: LoginInput!) {
    login(input: $input) {
      token
      expiresAtUtc
      player {
        id
        email
        displayName
        createdAtUtc
        startupPackClaimedAtUtc
        canClaimStartupPack
      }
    }
  }
`;
const ME_QUERY = `
  query {
    me {
      id
      email
      displayName
      createdAtUtc
      startupPackClaimedAtUtc
      canClaimStartupPack
    }
  }
`;
const MY_SUBSCRIPTION_QUERY = `
  query {
    mySubscription {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`;
const PROLONG_SUBSCRIPTION_MUTATION = `
  mutation ProlongSubscription($input: ProlongSubscriptionInput!) {
    prolongSubscription(input: $input) {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`;
const CLAIM_STARTUP_PACK_MUTATION = `
  mutation ClaimStartupPack {
    claimStartupPack {
      tier
      status
      isActive
      daysRemaining
      canProlong
      expiresAtUtc
      startsAtUtc
    }
  }
`;
export async function fetchGameServers() {
    const data = await gqlRequest(GAME_SERVERS_QUERY);
    return data.gameServers;
}
export async function registerAccount(email, displayName, password) {
    const data = await gqlRequest(REGISTER_MUTATION, {
        input: { email, displayName, password },
    });
    return data.register;
}
export async function loginAccount(email, password) {
    const data = await gqlRequest(LOGIN_MUTATION, {
        input: { email, password },
    });
    return data.login;
}
export async function fetchMe(token) {
    const data = await gqlRequest(ME_QUERY, undefined, token);
    return data.me;
}
export async function fetchMySubscription(token) {
    const data = await gqlRequest(MY_SUBSCRIPTION_QUERY, undefined, token);
    return data.mySubscription;
}
export async function prolongSubscription(token, months) {
    const data = await gqlRequest(PROLONG_SUBSCRIPTION_MUTATION, { input: { months } }, token);
    return data.prolongSubscription;
}
export async function claimStartupPack(token) {
    const data = await gqlRequest(CLAIM_STARTUP_PACK_MUTATION, undefined, token);
    return data.claimStartupPack;
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
`;
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
`;
const ADJUST_GOLD_TOKEN_MUTATION = `
  mutation AdjustGoldToken($input: AdjustGoldTokenInput!) {
    adjustGoldTokenBalance(input: $input) {
      playerId
      email
      displayName
      goldTokenBalance
    }
  }
`;
export async function fetchGoldTokenBalances(token) {
    const data = await gqlRequest(GOLD_TOKEN_BALANCES_QUERY, undefined, token);
    return data.goldTokenBalances;
}
export async function fetchGoldTokenTransactions(token, targetEmail, limit = 50) {
    const data = await gqlRequest(GOLD_TOKEN_TRANSACTIONS_QUERY, { targetEmail: targetEmail ?? null, limit }, token);
    return data.goldTokenTransactions;
}
export async function adjustGoldTokenBalance(token, targetEmail, amount, note) {
    const data = await gqlRequest(ADJUST_GOLD_TOKEN_MUTATION, { input: { targetEmail, amount, note: note ?? null } }, token);
    return data.adjustGoldTokenBalance;
}
