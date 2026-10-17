# Security audits

- **Cadence:** one report per ISO week, named `security-audit-YYYY-Www.md` (for example `security-audit-2026-W42.md`).
- **Reminder:** `.github/workflows/weekly-audit-reminder.yml` opens a "Weekly security audit reminder (YYYY-Www)" issue every Monday 07:00 UTC; close it when the report is merged.
- **Board and gate:** `.github/workflows/security-audit-board.yml` parses every `audits/*.md`. Each Open or In-Progress High/Critical finding must carry an `<!-- issue: #NNN -->` annotation or the "Security sign-off gate" check fails.
- **Format:** follow the latest report: audit question, summary, verification of prior findings, then numbered findings with Severity, Affected endpoint or mechanic, Risk description, Recommended fix and Status.
- **Older reports** use timestamped names (`security-audit-2026-07-07T201327.md`); keep them, they are history.
