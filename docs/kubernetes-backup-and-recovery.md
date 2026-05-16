# Kubernetes Backup and Recovery

This runbook describes the automated backup workflow and the restore scripts for Kubernetes workloads and PostgreSQL data.

## Daily backup workflow

Workflow: `.github/workflows/k8s-daily-backup.yml`

- Runs daily by cron (`02:17 UTC`) and can be executed manually.
- Backs up namespaces by scope:
  - `all` → `capitalism-stage*` and `capitalism-production*`
  - `stage` → `capitalism-stage*`
  - `production` → `capitalism-production*`
- Captures Kubernetes artifacts (core resources + cert-manager resources when available).
- Dumps PostgreSQL data from every `*-postgres` pod with `pg_dumpall`.
- Uploads one backup artifact per run with **7-day retention**.

## Restore workflow (operator runbook)

1. Download the backup artifact from a successful `k8s-daily-backup` run.
2. Extract it locally (or on an operations runner).
3. Restore Kubernetes artifacts:

```bash
./scripts/recovery/restore-k8s-artifacts.sh <backup-root-directory>
```

4. Restore PostgreSQL dumps:

```bash
./scripts/recovery/restore-postgres-data.sh <backup-root-directory>
```

## Notes

- The restore order is intentional: apply Kubernetes manifests first, then load database dumps.
- Database restore scripts skip missing pods and continue with remaining dumps.
- Always run restores from an operator workstation/runner that has `kubectl` access to the target cluster.
