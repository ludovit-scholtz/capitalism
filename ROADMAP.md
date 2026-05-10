# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Consumable Raw Materials & Resource Scarcity Mechanics

- [ ] Add a mine-side extraction history experience in building detail with a 30-day sparkline, depletion trendline, and an expanded dialog that explains reserve burn rate, expected depletion tick, and quality decay inflection points.

### Security Fairness Hardening

- [ ] Introduce API key scopes and enforcement gates so leaked keys cannot act as full-account impersonation; support read-only, bot-only, trading-only, and company-bound scopes with deny-by-default middleware enforcement and audit logging.
- [ ] Implement shard-verified ranking telemetry by validating `serverKey` against active server registration metadata, rejecting unknown or stale shard keys, and logging replay or duplicate event signatures for moderation review.
- [ ] Harden FX execution fairness by adding quote nonce, quote-issued timestamp, strict expiration, and explicit slippage tolerance so stale quotes and replayed execution payloads cannot extract better-than-market settlement.
- [ ] Expand building secondary market race-condition defenses using optimistic concurrency tokens on offer accept/cancel/buy paths and add parallel integration tests proving no double-fill or stale ownership transfer can occur.
- [ ] Strengthen loan collateral and foreclosure invariants by revalidating ownership, collateral lock, and payable balance at commit-time on every refinance, sale, destroy, and default transition path with overdue lifecycle tests.
- [ ] Add strict token-boundary tests and middleware assertions so MasterApi privilege cannot be granted from game-issued tokens, and impersonation is recognized only when actor and effective-player claims differ.
- [ ] Enforce bot company/account boundaries in every bot-eligible mutation by resolving ownership server-side, rejecting foreign-company identifiers, and adding a full negative test matrix across forex, building, lending, and stock actions.
- [ ] Complete ranking manipulation safeguards with idempotency keys, proof-reference deduplication, suspicious-pattern moderation queues, and admin review tooling that can quarantine telemetry batches before leaderboard publication.
- [ ] Keep frontend trust non-authoritative by validating all economy-sensitive fields server-side, rejecting client overrides for ownership, pricing, and timing values, and adding regression tests for tampered GraphQL payload attempts.
- [ ] Standardize object-authorization failure responses to "not found or not owned" semantics to reduce resource enumeration risk while preserving internal audit visibility through structured security logs.

### Security Operations & Audit Cadence

- [ ] Create a weekly security action board that mirrors `/audits/*.md` findings, tracks owner plus due tick, and blocks release sign-off when any High or Critical finding from the latest audit has no linked implementation issue.
- [ ] Add an automated GraphQL surface inventory report in CI that flags newly added finance, shareholder, ranking, lending, and admin queries or mutations missing explicit auth and ownership tests.
