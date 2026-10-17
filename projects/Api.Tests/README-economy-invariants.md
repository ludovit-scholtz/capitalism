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
entity ids; re-run that single seed to reproduce (all randomness comes from `new Random(seed)`).
The `Checker_Detects*` tests prove each invariant fails on a deliberately corrupted state.
