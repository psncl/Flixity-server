# Proposal

## Why

The API needs a low-cost production home with managed secrets and a safe way to apply schema changes.

## What Changes

- Host the API on one Azure App Service instance.
- Host Postgres on Azure Database for PostgreSQL Flexible Server.
- Configure secrets outside source control.
- Apply database migrations as a separate deploy step.
- CI/CD pipelines remain out of scope.

## Capabilities

### New Capabilities

None: this changes hosting, not API behaviour.

### Modified Capabilities

None. Set `skip_specs: true` when this change is fully specified, if that still holds.

## Impact

- Depends on `add-observability` and `add-movie-links-and-ratings`.
- Azure resources with costs after the free period.
- App configuration and deployment documentation.
