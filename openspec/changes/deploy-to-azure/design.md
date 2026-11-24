# Design

## Context

Based on October 2026 research: `research/postgres-hosting.md` on branch `origin/research/postgres-hosting`. Recheck prices before deploying. The migration step follows `add-city-lookup`.

- Flexible Server B1ms is free for 12 months (750 h/month, 32 GB storage, 32 GB backup), then about $16/month at East US prices.
- VNet access needs a Basic or higher App Service plan, a subnet and private DNS.
- Neon free suspends after 5 minutes idle, with 100 CU-h/month and 1 GB per project.
- Supabase free fits less well: it pauses after 7 days idle, and direct connections are IPv6-only.
- All are standard Postgres. Lock-in comes only from Entra authentication, Supabase APIs or Azure-only extensions.

## Goals / Non-Goals

**Goals:** low cost, portable Postgres, secrets outside source control.

**Non-Goals:** CI/CD, VNet, multiple instances, automatic scale-out.

## Decisions

- **API:** one App Service instance. The per-City lock relies on this.
- **Database:** Flexible Server Burstable B1ms, 32 GB. Keep the connection pool small because B1ms allows 35 user connections.
- **Network:** public endpoint allowlisting App Service outbound IPs, over TLS. Reject the "any Azure service" rule because it admits other tenants.
- **Secrets:** App Service settings, optionally Key Vault references. Keep Azure-only authentication optional for portability.
- **Migrations:** run a reviewed script or bundle before deploying the app, with schema permissions separate from the runtime user.
- **Spending:** set an OpenAI spending limit before going live.
- **After the free period:** Neon free is the fallback; move with `pg_dump` and `pg_restore`.

## Risks / Trade-offs

- [Outbound IPs can change] → check and update the allowlist.
- [Open-Meteo quota may be shared by outbound IP] → check after deployment; replace the geocoder if needed.
- [Rate limiting may see the proxy's IP] → configure and test forwarded headers.
- [Costs after the free period] → set an Azure budget alert.

## Migration Plan

Create resources, configure secrets and firewall, apply migrations, deploy, then verify featured Cities, a first search and a cached search. Roll back by redeploying the previous version; use a forward fix or point-in-time restore for the database.

## Open Questions

- App Service plan and region.
- Key Vault or plain App Settings at launch.
