# GitHub Actions Environments and Secrets for Kubernetes Deployments

This document defines how to set up GitHub Actions deployment environments for Capitalism and how to keep operational secrets out of the repository and workflow logs.

## Environment model

Create two GitHub Actions environments before enabling deployment workflows:

- `stage`
- `production`

Use `stage` for automatic deployments from `main`. Use `production` for public releases with approval rules.

Create the environments explicitly in GitHub first. Do not rely on workflow files to create them implicitly, because automatically created environments start without protection rules or secrets.

Recommended protection rules:

- `stage`: allow deployments from `main` without manual approval.
- `production`: restrict deployments to `main`, require reviewer approval, and prevent self-approval.

## Automated Kubernetes workflows

The Kubernetes deployment automation is implemented by these workflows:

- `.github/workflows/master-k8s-deploy-reusable.yml` — reusable master deployment contract (`workflow_call`) used by stage and production.
- `.github/workflows/deploy-stage-k8s.yml` — auto-triggered from `main` (and manual `workflow_dispatch`) to deploy:
  - `https://www.stage.capitalism5.com`
  - `https://api.stage.capitalism5.com`
- `.github/workflows/deploy-production-k8s.yml` — `workflow_dispatch` production deployment through the `production` environment gate; deploys:
  - `https://www.capitalism5.com`
  - `https://capitalism5.com` (canonical redirect to `www`)
  - `https://api.capitalism5.com`
- `.github/workflows/provision-game-shard-k8s.yml` — `workflow_dispatch` shard provisioning pipeline with inputs:
  - `game_name`
  - `city`
  - `environment` (`stage`/`production`)
  - `server_region`
  - `image_tag`

All workflows include actionable smoke-check failures and automatic rollback for deployment rollouts when smoke checks fail.

## Deployment topology

Expected public hostnames:

- Production master frontend: `www.capitalism5.com`
- Production master frontend redirect source: `capitalism5.com`
- Production master API: `api.capitalism5.com`
- Stage master frontend: `www.stage.capitalism5.com`
- Stage master API: `api.stage.capitalism5.com`
- Production game frontend: `{game-slug}.capitalism5.com`
- Production game API: `{game-slug}-api.capitalism5.com`
- Stage game frontend: `{game-slug}.stage.capitalism5.com`
- Stage game API: `{game-slug}-api.stage.capitalism5.com`

Ingress certificates must be issued through the cert-manager `ClusterIssuer` named `letsencrypt-dns`.

Certificate scope must match the actual zones:

- `capitalism5.com` and `*.capitalism5.com` for the production master endpoints
- `*.stage.capitalism5.com` for stage workloads
- `*.capitalism5.com` for production game shards

## What belongs in secrets vs variables

Use GitHub Actions environment secrets for anything private, personal, or credential-bearing.

Examples that belong in environment secrets:

- Kubernetes access credentials or cloud-auth bootstrap values
- PostgreSQL administrator passwords
- Per-environment JWT signing keys
- Master-server registration keys
- Game bootstrap administrator passwords
- Game administrator email addresses when they identify a real operator
- DNS provider API credentials used by the `letsencrypt-dns` issuer bootstrap flow

Use GitHub Actions environment variables only for non-sensitive values such as:

- public hostnames
- Kubernetes namespace names
- container registry image names
- feature flags that are safe to expose in deployment metadata

## Recommended secret layout

Keep shared infrastructure credentials at the smallest safe scope.

Recommended pattern:

- Repository or organization secrets: container registry credentials shared by all deployment workflows, if OIDC is not available.
- `stage` environment secrets: stage-only kube access, stage-only admin contacts, stage-only database bootstrap secrets.
- `production` environment secrets: production-only kube access, production-only admin contacts, production-only database bootstrap secrets.

Do not reuse the same secret value between `stage` and `production` unless there is a hard external requirement.

## Suggested secret names

These names are examples. Keep the names stable once workflows depend on them.

Environment secrets:

- `KUBE_CONFIG_DATA`
- `MASTER_DB_CONNECTION_STRING`
- `MASTER_JWT_SIGNING_KEY`
- `MASTER_REGISTRATION_KEY`
- `MASTER_ROOT_ADMIN_EMAIL`
- `GAME_BOOTSTRAP_ADMIN_EMAIL`
- `GAME_BOOTSTRAP_ADMIN_PASSWORD`
- `HARBOR_USERNAME`
- `HARBOR_PASSWORD`
- `MASTER_GRAPHQL_URL`
- `LETSENCRYPT_DNS_PROVIDER_TOKEN`

Environment variables:

- `MASTER_FRONTEND_HOST`
- `MASTER_API_HOST`
- `GAME_BASE_DOMAIN`
- `GAME_API_SUFFIX`
- `K8S_NAMESPACE_PREFIX`

## GitHub setup steps

### GitHub web UI

1. Open the repository on GitHub.
2. Go to `Settings`.
3. Open `Environments`.
4. Create `stage`.
5. Create `production`.
6. Add protection rules to `production`.
7. Add the required secrets to each environment.
8. Add the required non-sensitive variables to each environment.

### GitHub CLI

You can also set environment secrets and variables from the CLI.

```bash
gh secret set --env stage MASTER_ROOT_ADMIN_EMAIL
gh secret set --env production MASTER_ROOT_ADMIN_EMAIL
gh variable set --env stage MASTER_FRONTEND_HOST --body "www.stage.capitalism5.com"
gh variable set --env production MASTER_FRONTEND_HOST --body "www.capitalism5.com"
```

## Safe workflow usage

Never hardcode runtime secrets in workflow YAML, manifests, `.env.example`, or committed Helm values.

Prefer cloud OIDC federation for cluster authentication when the Kubernetes platform supports it. If that is not available yet, store kubeconfig or equivalent bootstrap credentials as environment secrets.

When a workflow needs a secret, pass it through the `secrets` context into the job environment and then create or update the Kubernetes `Secret` object directly.

Example:

```yaml
jobs:
  deploy:
    environment: production
    steps:
      - name: Apply runtime secret
        env:
          GAME_BOOTSTRAP_ADMIN_EMAIL: ${{ secrets.GAME_BOOTSTRAP_ADMIN_EMAIL }}
          GAME_BOOTSTRAP_ADMIN_PASSWORD: ${{ secrets.GAME_BOOTSTRAP_ADMIN_PASSWORD }}
        run: |
          printf '%s\n' "::add-mask::$GAME_BOOTSTRAP_ADMIN_EMAIL"
          kubectl create secret generic game-runtime \
            --namespace "$K8S_NAMESPACE" \
            --from-literal=SeedData__AdminEmail="$GAME_BOOTSTRAP_ADMIN_EMAIL" \
            --from-literal=SeedData__AdminPassword="$GAME_BOOTSTRAP_ADMIN_PASSWORD" \
            --dry-run=client -o yaml | kubectl apply -f -
```

Rules for safe workflow authoring:

- Never `echo` a secret value to the logs.
- Mask any sensitive runtime value with `::add-mask::` if it did not originate from the GitHub `secrets` context.
- Do not pass secrets through workflow inputs when the value can instead be stored as an environment secret.
- Do not write secret values into committed manifests. Write them into Kubernetes `Secret` objects at deploy time.
- Treat operator email addresses as sensitive when they identify a real person or administrative contact.

## New game-server provisioning workflow

The dedicated game-provisioning workflow should accept non-sensitive inputs such as:

- `game_name`
- `environment`
- `server_region`

The workflow should generate or securely fetch sensitive values at runtime:

- PostgreSQL database name and user password
- game API JWT signing key
- shard-scoped registration secret value
- bootstrap administrator password

Generated secrets should be written directly to Kubernetes `Secret` objects or a managed secret store. They should not be committed back into the repository.

## Rotation and audit rules

- Rotate stage secrets whenever a non-production credential is exposed.
- Rotate production secrets through a scheduled maintenance procedure with rollback notes.
- Keep separate secret values per environment.
- Review environment secrets after every infrastructure ownership change.
- Remove unused secrets when workflows or services are retired.
- When rotating `MASTER_REGISTRATION_KEY`, rotate it in both master and shard deployment environments in a coordinated maintenance window.
- For shard reprovisioning, always re-generate per-shard DB/JWT/admin credentials; never copy credentials from a previous shard.

## Minimum review checklist

Before merging deployment workflow changes, confirm all of the following:

- `stage` and `production` environments already exist in GitHub.
- `production` has reviewer protection enabled.
- No administrator email or password is committed in workflow YAML, manifests, or docs examples.
- Kubernetes secrets are created from GitHub environment secrets at runtime.
- Stage deploys from `main` automatically.
- Production deploys only through the protected environment flow.
- TLS uses the `letsencrypt-dns` cluster issuer and matches the real domain zone.
- Stage/production workflow summaries include environment, image tag, and hostnames.
- Provisioning runs include shard slug, hostnames, and smoke-check status in the job summary.
