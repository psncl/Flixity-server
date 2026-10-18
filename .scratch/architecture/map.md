# Map: Flixity backend architecture

Label: wayfinder:map

## Destination

A written backend architecture spec for Flixity, ready to build from: ADRs (in `docs/adr/`), a data model replacing `Entities.md`, `GLOSSARY.md`, and the API contract the frontend will consume. Target: Azure App Service + Postgres. Implementation is a separate effort.

## Notes

- Domain: .NET 10 minimal API, vertical slices; see `CLAUDE.md`, `Requirements.md`, `GLOSSARY.md`.
- Fixed constraints (user decisions, not up for debate): **Postgres** (not SQL Server, to stay host-portable), **Dapper** (not EF Core), **Azure App Service** (free trial) for the API.
- Learning project / portfolio, low traffic; prefer simple choices with a cheap path to scale.
- Owner is a junior .NET dev: explain non-obvious tradeoffs briefly.
- Grilling tickets: call the `grilling` and `domain-modeling` skills; update `GLOSSARY.md` and offer ADRs as decisions land.
- Tracker: local markdown (`.scratch/architecture/issues/`).

## Decisions so far

<!-- one line per resolved ticket: [title](issues/NN-slug.md): gist -->

- [What external links and ratings can we actually get?](issues/03-links-and-ratings.md): all ratings (IMDb, RT, Metacritic) from OMDb; links built from imdbID plus one keyless Wikidata lookup for RT/Metacritic/Letterboxd/MUBI IDs, search-URL fallback; no TMDb for now.
- [How are schema migrations run with Dapper + Postgres?](issues/04-dapper-migrations.md): DbUp + dbup-postgresql, plain forward-only SQL scripts run at app startup; move to a deploy step when scaling out. FluentMigrator if real rollback is wanted; avoid Evolve.
- [Where does Postgres run alongside Azure App Service?](issues/05-postgres-hosting.md): Azure Flexible Server B1ms (free 12 months, then ~$16/mo), public endpoint + IP allowlist, connection string in App Settings/Key Vault; Neon free as post-trial fallback.
- [What observability tooling fits a .NET 10 API on Azure App Service?](issues/06-observability-tooling.md): Microsoft.OpenTelemetry distro, OTLP to Aspire dashboard in dev and Azure Monitor in prod; OMDb/Gemini auto-tracked as HTTP spans (no token data), add the Npgsql source; free 5 GB/month with sampling + daily cap.

## Not yet specified

- Where data access lives in the vertical slices (per-feature queries vs shared repository) and how Gemini/OMDb clients get DI-registered consistently.
- Testing strategy against Postgres (Testcontainers? in-memory fakes?).
- Source of the curated UK/Europe city list for the dropdown (hardcoded, seeded, pre-generated).
- How many movies a search returns (requirements say 5 or 10; code asks Gemini for 20 and filters).
- Whether to use Gemini's Google Search grounding to reduce hallucinated Filming Locations.

## Out of scope

- User accounts and favourites.
- Admin / curation UI for correcting bad results.
- Frontend tech stack (only the API contract is in scope).
- CI/CD pipelines.
- Regions/landscapes as search targets (only Cities).
