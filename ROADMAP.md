# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Onboarding

- [x] (100%) The generated user name - the personal account name is not stored properly. Make sure to store it in the master server if the server does not already contain this information.
- [x] (100%) Generated user personal account name is not used in the ranking. Make sure to use it in the ranking.
- [x] (100%) Do not show the jwt user name anywhere to other players. The user name is generated from the user's algorand address, so for the privacy purposes it is not good to use it

### Consumable Raw Materials & Resource Scarcity Mechanics

- [x] (100%) Add a mine-side extraction history experience in building detail with a 30-day sparkline, depletion trendline, and an expanded dialog that explains reserve burn rate, expected depletion tick, and quality decay inflection points.

### Security Fairness Hardening

- [x] (100%) Introduce API key scopes and enforcement gates so leaked keys cannot act as full-account impersonation; support read-only, bot-only, trading-only, and company-bound scopes with deny-by-default middleware enforcement and audit logging.
- [x] (100%) Implement shard-verified ranking telemetry by validating `serverKey` against active server registration metadata, rejecting unknown or stale shard keys, and logging replay or duplicate event signatures for moderation review.
- [x] (100%) Harden FX execution fairness by adding quote nonce, quote-issued timestamp, strict expiration, and explicit slippage tolerance so stale quotes and replayed execution payloads cannot extract better-than-market settlement.
- [x] (100%) Expand building secondary market race-condition defenses using optimistic concurrency tokens on offer accept/cancel/buy paths and add parallel integration tests proving no double-fill or stale ownership transfer can occur.
- [x] (100%) Strengthen loan collateral and foreclosure invariants by revalidating ownership, collateral lock, and payable balance at commit-time on every refinance, sale, destroy, and default transition path with overdue lifecycle tests.
- [x] (100%) Add strict token-boundary tests and middleware assertions so MasterApi privilege cannot be granted from game-issued tokens, and impersonation is recognized only when actor and effective-player claims differ.
- [x] (100%) Enforce bot company/account boundaries in every bot-eligible mutation by resolving ownership server-side, rejecting foreign-company identifiers with `NOT_FOUND_OR_NOT_OWNED`, auditing rejected API-key attempts, and covering forex, building, lending, and stock violations with regression tests.
- [x] (100%) Complete ranking manipulation safeguards with idempotency keys, proof-reference deduplication, suspicious-pattern moderation queues, and admin review tooling that can quarantine telemetry batches before leaderboard publication.
- [x] (100%) Keep frontend trust non-authoritative by validating economy-sensitive stock trade ownership overrides server-side, rejecting tampered account-type/company payloads with `INVALID_CLIENT_OVERRIDE`, and adding backend plus Playwright regressions proving friendly client-error handling.
- [ ] (55%) Standardize object-authorization failure responses to "not found or not owned" semantics to reduce resource enumeration risk while preserving internal audit visibility through structured security logs.

### Security Operations & Audit Cadence

- [ ] Create a weekly security action board that mirrors `/audits/*.md` findings, tracks owner plus due tick, and blocks release sign-off when any High or Critical finding from the latest audit has no linked implementation issue.
- [ ] Add an automated GraphQL surface inventory report in CI that flags newly added finance, shareholder, ranking, lending, and admin queries or mutations missing explicit auth and ownership tests.
