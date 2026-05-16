# Capitalism online game

This is attempt to create online mmorpg version of the capitalism game.

## Product roadmap

[ROADMAP.md](ROADMAP.md)

## Kubernetes deployment automation

- Stage master deployment workflow: `.github/workflows/deploy-stage-k8s.yml` (auto deploy from `main`)
- Production master deployment workflow: `.github/workflows/deploy-production-k8s.yml` (`workflow_dispatch`, approval-gated through `production` environment)
- Game shard provisioning workflow: `.github/workflows/provision-game-shard-k8s.yml` (`workflow_dispatch`)
- Daily Kubernetes backup workflow: `.github/workflows/k8s-daily-backup.yml` (scheduled, 7-day retention)
- Environment/secret setup, rotation, and log-masking rules: [`docs/github-actions-environments-and-secrets.md`](docs/github-actions-environments-and-secrets.md)
- Backup/restore runbook and recovery scripts: [`docs/kubernetes-backup-and-recovery.md`](docs/kubernetes-backup-and-recovery.md)

## Security configuration

- Required secure environment variables are listed in `.env.example`.
- For non-Development deployments of `projects/Api`, set:
  - `ConnectionStrings__GameCatalog`
  - `Jwt__SigningKey` (strong 32+ character secret)
  - `SeedData__AdminPassword` (required whenever `Auth__PasswordAuthEnabled=true`; must not be a placeholder)
- For non-Development deployments of `projects/MasterApi`, set:
  - `ConnectionStrings__MasterCatalog`
  - `Jwt__SigningKey` (strong 32+ character secret)
  - `GameAdministration__RootAdministratorEmails__0` (plus indexed entries for additional root admins)
- Startup now fails fast outside Development/Testing if any of the above values are missing or placeholder values.
- Recommended secret providers for production: Azure Key Vault, Docker secrets, or Kubernetes Secrets (inject into environment variables at runtime).
- Browser authentication now uses server-issued session cookies (`auth_token`) with `HttpOnly`, `SameSite=Strict`, and `Secure` (outside Development) flags instead of persisting JWTs in `localStorage` or `sessionStorage`.
- Frontend GraphQL requests use `credentials: include` cookie sessions; browser code no longer injects `Authorization: Bearer` headers for normal player sessions.
