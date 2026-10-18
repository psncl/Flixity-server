# Where should Postgres run for Flixity (alongside Azure App Service)?

Research date: 2026-10-18. Every price below was checked on that date. Prices change, so check them again before you commit to a plan.

Context: a .NET 10 minimal API on Azure App Service (Azure free trial), Npgsql + Dapper, low traffic, learning/portfolio project. The owner wants plain Postgres so the database can move to another host later.

## 1. Options at a glance

| | Azure DB for PostgreSQL Flexible Server (B1ms) | Neon | Supabase |
|---|---|---|---|
| Free allowance | 12 months: 750 h/month B1ms + 32 GB storage + 32 GB backup | Permanent free plan: 100 CU-hours/project/month, 1 GB storage/project (20 GB total) | Permanent free plan: 500 MB DB, 2 active projects |
| Cost after the free period | ~$12.41/month compute + ~$3.68/month for 32 GB storage, about **$16/month** (East US list price) | $0 if you stay within the free limits. Launch plan is pay as you go: $0.106/CU-hour, $0.35/GB-month | $0 on free. Pro starts at $25/month |
| Idle behaviour | Always on (manual stop allowed for at most 7 days) | Suspends after 5 min idle. Wakes in "a few hundred ms" | Pauses after 1 week of inactivity (manual restore) |
| Network from App Service | Public endpoint + IPv4 firewall, or VNet (private access) | Public endpoint, TLS required | Direct connection is IPv6-only. From IPv4-only hosts, use the Supavisor pooler |
| Passwordless (Entra / managed identity) | Yes | No (password in connection string) | No (password in connection string) |
| Lock-in | Plain community Postgres, with an allowlist of extensions | Plain Postgres | Plain Postgres plus optional platform extras (Auth, Storage, etc.) |

## 2. Azure Database for PostgreSQL Flexible Server

### Cost during the trial
- The Azure free account includes Flexible Server **free for 12 months**. Each month you get **750 hours of Burstable B1MS**, 32 GB storage and 32 GB backup storage. 750 hours is enough to run one server around the clock. You only pay if you go over these limits. The portal still shows an "Estimated cost per month", which you can ignore while you stay inside the limits. Source: https://learn.microsoft.com/azure/postgresql/configure-maintain/how-to-deploy-on-azure-free-account
- To stay free you have to pick **B1ms with storage ≤ 32 GB**. Any other SKU, or any extra storage, is billed (same source).
- Free-account mechanics: the $200 credit lasts 30 days. At the end of the 30 days, or once the credit runs out, the subscription is **disabled unless you upgrade to pay-as-you-go**. After upgrading, you keep the 12-month free services and pay only for usage above the free amounts. Sources: https://learn.microsoft.com/azure/cost-management-billing/manage/avoid-charges-free-account, https://learn.microsoft.com/azure/cost-management-billing/manage/create-free-services

### Cost after the 12 months (list prices, East US, USD, pay-as-you-go)
Pulled from the Azure Retail Prices API (https://prices.azure.com/api/retail/prices) on 2026-10-18. The marketing pricing page (https://azure.microsoft.com/pricing/details/postgresql/flexible-server/) showed no figures when it was fetched.
- B1MS compute: **$0.017/hour**, about $12.41/month at 730 h
- B2S compute: $0.068/hour, about $49.64/month (a comparison point if B1ms turns out too small)
- Storage ("Flex Server Storage – Data Stored"): **$0.115/GB-month**. The 32 GB minimum comes to about $3.68/month
- Backup storage LRS: $0.095/GB-month, for backup beyond any included amount. Check the pricing page for how much backup storage is included
- **Total for the smallest server after the free year: about $16/month.**
- Stopping the server stops compute billing immediately, but storage is still billed. A stopped server **auto-starts after 7 days**. Sources: https://learn.microsoft.com/azure/postgresql/overview, https://learn.microsoft.com/azure/postgresql/configure-maintain/how-to-stop-server

### B1ms caveats
- Specs: 1 vCore, 2 GiB, 640 IOPS. **50 max connections, 35 of them for user connections.** Keep the Npgsql pool small (`Maximum Pool Size` of around 10–20). Sources: https://learn.microsoft.com/azure/postgresql/compute-storage/concepts-compute, https://learn.microsoft.com/azure/postgresql/configure-maintain/concepts-limits
- Burstable uses a CPU credit model. If credits run out, the server drops to baseline CPU. Microsoft says the tier is "not recommended for production" and "doesn't qualify for 24/7 support". That's acceptable for a portfolio app (same source).

### Connectivity from App Service
- **Public access (allowed IPs):** the firewall blocks everything by default. You can either:
  - allowlist the App Service outbound IPs, which you can see in the app's properties (`outboundIpAddresses` / `possibleOutboundIpAddresses`; https://learn.microsoft.com/azure/app-service/overview-inbound-outbound-ips), or
  - tick **"Allow public access from any Azure service within Azure to this server"**, which is a 0.0.0.0 rule. Microsoft warns that this rule allows connections from **any Azure customer's** subscription, so security then rests on credentials alone.
  - Firewall rules must be IPv4.
  - Source: https://learn.microsoft.com/azure/postgresql/security/security-firewall-rules, https://learn.microsoft.com/azure/postgresql/network/concepts-networking-public
- **Private access (VNet):** the server gets no public endpoint. The App Service app needs **regional VNet integration**, which requires a **Basic tier or higher** App Service plan (not Free F1 or Shared). VNet integration itself costs nothing extra. Source: https://learn.microsoft.com/azure/app-service/overview-vnet-integration. Private access also means a delegated subnet and a private DNS zone. That's more setup than a learning project needs, and it rules out the free App Service tier.
- **TLS:** enforced by default (`require_secure_transport=ON`). Only TLS 1.2 and 1.3 are accepted. Microsoft recommends `sslmode=verify-full`/`verify-ca` and keeping the full set of Azure root CAs (DigiCert Global Root G2, Microsoft RSA Root CA 2017) in the trust store. **Don't pin intermediate or server certificates**: Microsoft rotated intermediates from Nov 2025 to Jan 2026 without notice. For App Service apps that use the platform certificate store, no action is needed. Sources: https://learn.microsoft.com/azure/postgresql/security/security-tls, https://learn.microsoft.com/azure/postgresql/security/security-tls-how-to-connect

### Secret handling (three levels)
1. **App Settings / Connection strings in App Service.** The simplest option. They're exposed to the app as environment variables, so ASP.NET Core picks them up through `IConfiguration` (`ConnectionStrings__Default`). Locally, user secrets keep working without any change.
2. **Key Vault references.** You set an App Setting to `@Microsoft.KeyVault(SecretUri=...)`. App Service resolves it using the app's managed identity, which needs the *Key Vault Secrets User* role. **No code changes are needed.** If a reference fails to resolve, the app receives the literal `@Microsoft.KeyVault(...)` string. Source: https://learn.microsoft.com/azure/app-service/app-service-key-vault-references. Cost: Key Vault Standard charges $0.03 per 10K operations (Retail Prices API, 2026-10-18), which is effectively free here.
3. **Managed identity + Microsoft Entra auth (passwordless).** There's no password at all. You enable Entra auth on the server (this restarts it), make an Entra admin, and create a DB role mapped to the App Service identity. The app then gets tokens for `https://ossrdbms-aad.database.windows.net/.default`. For Npgsql, Microsoft ships **`Microsoft.Azure.PostgreSQL.Auth`** (v1.0.0), which adds `NpgsqlDataSourceBuilder.UseEntraAuthentication(new DefaultAzureCredential())` and refreshes tokens per connection. Locally, `DefaultAzureCredential` uses your `az login` / Visual Studio identity. Sources: https://learn.microsoft.com/azure/postgresql/security/security-entra-concepts, https://learn.microsoft.com/azure/postgresql/security/security-entra-configure, https://learn.microsoft.com/azure/app-service/tutorial-connect-msi-azure-database, https://learn.microsoft.com/dotnet/api/overview/azure/microsoft.azure.postgresql.auth-readme
   - Tradeoff: this is **Azure-only** code and setup. If the DB moves to Neon, Supabase or self-hosted later, you go back to a password connection string. Keep the choice behind configuration (for example, use Entra auth only when there's no password in the connection string) so moving stays a config change.

## 3. Neon (serverless Postgres)

Source for this section: https://neon.com/pricing and https://neon.com/docs/introduction/scale-to-zero (fetched 2026-10-18).
- **Free plan (permanent, no card):**
  - 100 CU-hours per project per month, autoscaling up to 2 CU (8 GB RAM)
  - 1 GB storage per project, 20 GB total
  - 100 projects, 10 branches/project
  - 5 GB egress per project
  - If you run out of CU-hours or egress, compute is suspended until the next month. Data isn't deleted.
- **Scale to zero:** compute suspends after **5 minutes idle**. On the free plan you can't disable this. It wakes on the next query "within a few hundred milliseconds". For a low-traffic app, the first request after idle pays that latency, plus TCP/TLS setup.
- **Launch plan (pay as you go, no minimum):**
  - $0.106/CU-hour, $0.35/GB-month storage
  - 500 GB egress included
  - Scale-to-zero can be turned off
  - Invoices under $0.50 aren't collected
  - Rough example: a 0.25 CU compute that is awake 8 h/day comes to about 60 CU-h/month, which is within the free 100 CU-h. Always-on 0.25 CU is about 182 CU-h, roughly $19/month on Launch.
- **Connectivity:** public endpoint, TLS required (`SSL Mode=Require`). Works from App Service over IPv4 without a VNet. Neon offers a pooled (`-pooler`) hostname, but Npgsql already pools connections in-process, so the direct endpoint is fine for a long-running App Service process.
- **Secrets:** password connection string, kept in App Settings or a Key Vault reference. No Entra/managed-identity auth.
- **Portability:** standard Postgres, so `pg_dump`/`pg_restore` works both ways.

## 4. Supabase

Source for this section: https://supabase.com/pricing and https://supabase.com/docs/guides/database/connecting-to-postgres (fetched 2026-10-18).
- **Free plan:** 500 MB database per project, up to 2 active projects, 5 GB egress. **Projects pause after 1 week of inactivity.** A portfolio site that nobody visits for a week goes offline until you restore it.
- **Pro:** from $25/month, which includes $10 compute credit (one Micro instance: 1 GB RAM, 60 direct / 200 pooler connections).
- **IPv6 gotcha:**
  - The direct connection (`db.<ref>.supabase.co:5432`) is **IPv6 only** unless you buy the IPv4 add-on.
  - App Service outbound traffic is IPv4, so you'd use the **shared Supavisor pooler in session mode** (port 5432, IPv4).
  - Transaction mode (port 6543) breaks prepared statements and session state, so avoid it for a persistent server.
  - Use `sslmode=require` or stricter.
- **Secrets:** password connection string only.
- **Portability:** standard Postgres underneath. Lock-in only applies if you adopt Supabase Auth, Storage, RLS-centric APIs and so on.

## 5. Similar options (not researched in depth)
Other hosted Postgres services with free tiers exist, such as Aiven, Render, Railway and Xata. Their free tiers change often, and several expire or sleep. They offer nothing Neon doesn't already cover for this use case, so they weren't researched further.

## 6. Ease of moving hosts later
- All three run community PostgreSQL, so moving means `pg_dump`/`pg_restore` (or logical replication) plus changing one connection string.
- What actually creates lock-in is code and setup around the database:
  - Azure Entra token auth
  - Supabase platform APIs
  - Azure-only extensions (Azure uses an `azure.extensions` allowlist)
- Keeping all SQL in Dapper and the schema in plain migration scripts keeps the database portable.
- To keep the app host-agnostic, build one `NpgsqlDataSource` from `ConnectionStrings:Default` in `ConfigureServices.cs`. The host then only affects configuration.

## 7. Recommendation

- **Now (during the trial and the 12-month free window): Azure Database for PostgreSQL Flexible Server, Burstable B1ms, 32 GB, public access.**
  - It's free for 12 months, has no cold starts and lives in the same region as the App Service app.
  - It gives you practice with the Azure-native setup, which is a good portfolio point.
  - Firewall: allowlist the App Service outbound IPs rather than the "any Azure service" 0.0.0.0 rule.
  - TLS: use `SSL Mode=VerifyFull` (or `Require` to start).
  - Npgsql pool size: keep `Maximum Pool Size` small because B1ms allows only 35 user connections.
- **Secrets:**
  - Start with an App Service *connection string* setting (no code change, and user secrets locally).
  - Optionally move the password to a **Key Vault reference** (still no code change).
  - Treat **managed identity + Entra auth** (`Microsoft.Azure.PostgreSQL.Auth`) as an optional learning step. It's Azure-only, so make it conditional on configuration.
  - Skip VNet/private access. It needs a Basic+ App Service plan, a subnet and private DNS, which is too much overhead for this project.
- **Before month 12 (set a calendar reminder):**
  - Either keep Azure at about $16/month, or `pg_dump` to the **Neon free plan**. Neon is permanently free at this scale; the cost is a cold start of a few hundred ms after 5 minutes idle.
  - Supabase free is the weakest fit: the 1-week pause and the IPv6-only direct connection both count against it.
