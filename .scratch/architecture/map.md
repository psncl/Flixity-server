# Map: Flixity backend architecture

Label: wayfinder:map

**Continued in OpenSpec (2026-11-24).** This map is now a historical record. Current plans are in `openspec/changes/`, and win where they differ. Decision reasons live in each `design.md` instead of ADRs.

## Destination

A written backend architecture spec for Flixity, ready to build from: ADRs (in `docs/adr/`), a data model replacing `Entities.md`, `GLOSSARY.md`, and the API contract the frontend will consume. Target: Azure App Service + Postgres. Implementation is a separate effort.

## Notes

- Domain: .NET 10 minimal API, vertical slices; see `CLAUDE.md`, `Requirements.md`, `GLOSSARY.md`.
- Fixed constraints (user decisions, not up for debate): **Postgres** (not SQL Server, to stay host-portable), **EF Core** (replaced Dapper on 2026-11-24), **Azure App Service** (free trial) for the API.
- Learning project / portfolio, low traffic; prefer simple choices with a cheap path to scale.
- Owner is a junior .NET dev: explain non-obvious tradeoffs briefly.
- Grilling tickets: call the `grilling` and `domain-modeling` skills; update `GLOSSARY.md` and offer ADRs as decisions land.
- Tracker: local markdown (`.scratch/architecture/issues/`).

## Decisions so far

<!-- one line per resolved ticket: [title](issues/NN-slug.md): gist -->

- [What external links and ratings can we actually get?](issues/03-links-and-ratings.md): all ratings (IMDb, RT, Metacritic) from OMDb; links built from imdbID plus one keyless Wikidata lookup for RT/Metacritic/Letterboxd/MUBI IDs, search-URL fallback; no TMDb for now.
- [How are schema migrations run with Dapper + Postgres?](issues/04-dapper-migrations.md): **superseded by EF Core Migrations, run as a deploy step.** Originally: DbUp + dbup-postgresql, plain forward-only SQL scripts run at app startup; move to a deploy step when scaling out. FluentMigrator if real rollback is wanted; avoid Evolve.
- [Where does Postgres run alongside Azure App Service?](issues/05-postgres-hosting.md): Azure Flexible Server B1ms (free 12 months, then ~$16/mo), public endpoint + IP allowlist, connection string in App Settings/Key Vault; Neon free as post-trial fallback.
- [What observability tooling fits a .NET 10 API on Azure App Service?](issues/06-observability-tooling.md): Microsoft.OpenTelemetry distro, OTLP to Aspire dashboard in dev and Azure Monitor in prod; OMDb/Gemini auto-tracked as HTTP spans (no token data), add the Npgsql source; free 5 GB/month with sampling + daily cap.

- [What role does the database play?](issues/01-database-role.md): permanent, lazily filled cache.
- [How does free-text input resolve to one canonical City?](issues/02-city-identity.md): geocoder suggestions, user picks, GeoNames ID.
- [How are Blurbs generated?](issues/07-blurb-generation.md): with the titles in one LLM call, no web search.
- [What happens when a City is searched for the first time?](issues/08-new-city-search-flow.md): synchronous; partial results are not saved.
- [What is the data model?](issues/09-data-model.md): City, Movie, FilmingLocation.
- [What counts as a failed search, and where is it recorded?](issues/10-failed-search-recording.md): logs and metrics only.
- [What is the API contract for the frontend?](issues/11-api-contract.md): featured cities, city suggestions, city search.

## Previously unspecified, now decided

- Data access: `DbContext` directly in handlers. LLM access: `IChatClient`, provider chosen in DI, starting with OpenAI `gpt-6-luna` instead of Gemini.
- Testing: Testcontainers Postgres; external APIs faked.
- Curated cities: rows seeded through migrations.
- Movie count: ask for 15, keep up to 10 that OMDb confirms.
- Search grounding: not used, because of cost.

## Out of scope

- User accounts and favourites.
- Admin / curation UI for correcting bad results.
- Frontend tech stack (only the API contract is in scope).
- CI/CD pipelines.
- Regions/landscapes as search targets (only Cities).
