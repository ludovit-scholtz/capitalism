# Economy invariant suite

`EconomyInvariantTests` drives the real `TickProcessor` plus seeded random player actions
(`transferFunds`, supply-contract propose/accept/reject/cancel) over several players and asserts, every 10 steps
and at the end (`EconomyInvariantChecker`):

1. **No negative balances** on non-government bank accounts.
2. **Building ownership** - every building has exactly one existing owning company.
3. **Matched ledger** - each ledger row naming a bank account points at an existing account of the same company.
4. **Conservation** - per company, the change in total bank balances since the baseline equals the sum of its
   new ledger rows (no money appears or vanishes without a bank-statement row).

Run: `dotnet test projects/Api.Tests --filter FullyQualifiedName~EconomyInvariantTests`.
Add a seed to the `[InlineData]` list to widen coverage. A failure prints `seed`, `step`, `tick` and the offending
entity ids; re-run that single seed to reproduce. The action sequence is fully determined by `new Random(seed)`; tick-phase internals (NPC phases, `Random.Shared` account numbers, wall-clock timestamps) are not seeded, so a rare failure may need a few re-runs. A scripted propose/accept/buyer-cancel runs first in every seed so the cancellation-fee path is always covered. NPC companies are not part of the seeded world yet (tracked in issue #607).
The `Checker_Detects*` tests prove each invariant fails on a deliberately corrupted state.
