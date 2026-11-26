# Cinema Location

Backend for an app that lists movies by shooting location (city), for travel inspiration. See `Requirements.md` for features, `Entities.md` for the data model and `GLOSSARY.md` for domain terms. `Notes.md` holds the author's learning notes and tradeoffs.

The owner is a junior .NET developer building this to learn. Explain non-obvious choices briefly.

## Stack

- .NET 10 ASP.NET Core minimal API (`src/server.csproj`), OpenAPI + Scalar for docs
- `Microsoft.Extensions.AI` (`IChatClient`) to get movies for a City; provider and model set in `appsettings.json` (OpenAI `gpt-6-luna`)
- OMDb API for movie metadata and ratings, and to verify that suggested movies exist
- Wikidata SPARQL (no key; User-Agent in `appsettings.json`) for movies' Rotten Tomatoes, Metacritic, Letterboxd and MUBI IDs
- Secrets via ASP.NET Core user secrets
- Postgres with EF Core (Npgsql provider 10.0.x; 11.x needs .NET 11); local setup in `README.md`
- Open-Meteo geocoding to identify Cities; built-in `AddValidation()` and ProblemDetails
- Tests: xUnit, FluentAssertions, NSubstitute; Testcontainers for integration tests (needs Docker)

Planned, not yet in code (see `openspec/changes/`):

- Running migrations as a deploy step
- Azure App Service hosting

## Planning

Plans live in `openspec/`: current behaviour in `specs/`, planned changes in `changes/`, finished ones in `changes/archive/`. `add-city-lookup` and `add-city-movie-search` are done. Build the rest in this order: `add-movie-links-and-ratings` and `add-observability`, then `deploy-to-azure`. These have a proposal and design only, so write their specs and tasks (as applicable) before building.

## Structure

Vertical slice architecture: code is grouped by feature under `src/Features/`, shared abstractions in `src/Common/Api/`.

- `SearchCities`, `GetFeaturedCities`, `SearchCityMovies`: endpoints, each with a handler
- `GeocodeOpenMeteo`: geocoder client (`ICityGeocoder`)
- Database model, seed data and migrations: `src/Common/Database/`
- `SearchMoviesLlm`: LLM client (`IMovieInfoLlmClient`)
- `SearchMoviesOmdb`: OMDb client (`IMovieInfoClient`)
- `LookupSiteIdsWikidata`: Wikidata client (`IMovieSiteIdClient`); returns no IDs on failure instead of throwing
- Endpoints are registered in `src/Endpoints.cs`; services in `src/ConfigureServices.cs`
- Tests: `tests/UnitTests`, `tests/IntegrationTests`

## Current state

- City movie search in place: GeoNames ID -> saved result, or LLM suggests movies with blurbs -> OMDb verifies and fills in metadata -> saved permanently and returned. Partial results are not saved.
- City lookup in place: suggestions from Open-Meteo, featured Cities from Postgres.
- Not started: external links and ratings, observability, deployment, frontend.

## Commands

- Build: `dotnet build src/server.sln`
- Test: `dotnet test tests/UnitTests`, `dotnet test tests/IntegrationTests`
- New migration: `dotnet ef migrations add <Name> --project src/server.csproj --output-dir Common/Database/Migrations`
