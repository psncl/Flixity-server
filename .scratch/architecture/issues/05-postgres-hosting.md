# Where does Postgres run alongside Azure App Service?

Type: research
Status: resolved
Blocked by: 

## Question

Compare Azure Database for PostgreSQL Flexible Server (free-trial / free-tier allowance), Neon, Supabase and similar for a low-traffic learning project: cost during and after the Azure trial, connectivity from App Service, connection string/secret handling (Key Vault vs App Settings), and ease of moving hosts later.

## Answer

- **Azure Database for PostgreSQL Flexible Server, Burstable B1ms, 32 GB.** Free for 12 months on the Azure free account (750 h/month B1ms, 32 GB storage, 32 GB backup); then ~$16/month ($0.017/h compute + $0.115/GB-month storage, East US list price, 2026-10-18). No cold starts. B1ms allows only 35 user connections, so keep the Npgsql pool small.
- **Connectivity:** public endpoint with App Service's outbound IPv4s allowlisted (not the 0.0.0.0 "any Azure service" rule, which admits other tenants). TLS 1.2+ enforced by default. Skip VNet/private access (needs Basic+ App Service plan, subnet, private DNS).
- **Secrets:** App Service connection-string setting, optionally upgraded to a Key Vault reference (no code change). Managed identity + Entra auth (`Microsoft.Azure.PostgreSQL.Auth`, `UseEntraAuthentication`) is passwordless but Azure-only: keep it config-switchable.
- **Fallback after year one: Neon free** (permanent; 100 CU-h/month, 1 GB/project; suspends after 5 min idle, wakes in a few hundred ms). Supabase free is a poorer fit: pauses after 7 days idle, direct connection IPv6-only.
- **Portability:** all are vanilla Postgres; moving is `pg_dump`/`pg_restore` + new connection string. Lock-in only via Entra auth, Supabase APIs, or Azure-only extensions.

Findings: branch `research/postgres-hosting`, file `research/postgres-hosting.md`
