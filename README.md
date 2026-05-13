# Capitalism online game

This is attempt to create online mmorpg version of the capitalism game.

## Product roadmap

[ROADMAP.md](ROADMAP.md)

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
