# Cinema Location

Backend for an app that lists movies by shooting location (city), for travel inspiration. See `Requirements.md` for features, `Entities.md` for the planned data model and `GLOSSARY.md` for domain terms. `Notes.md` holds the author's learning notes and tradeoffs.

The owner is a junior .NET developer building this to learn. Explain non-obvious choices briefly.

## Stack

- .NET 10 ASP.NET Core minimal API (`src/server.csproj`), OpenAPI + Scalar for docs
- Google Gen AI .NET SDK (Gemini) to get movies for a location (chosen over MS Agent Framework for simplicity)
- OMDb API for full movie metadata
- Secrets via ASP.NET Core user secrets
- Postgres with EF Core (Npgsql provider 10.0.x; 11.x needs .NET 11); local setup in `README.md`
- Open-Meteo geocoding to identify Cities; built-in `AddValidation()` and ProblemDetails
- Tests: xUnit, FluentAssertions, NSubstitute; Testcontainers for integration tests (needs Docker)

Planned, not yet in code (see `openspec/changes/`):

- Running migrations as a deploy step
- `Microsoft.Extensions.AI` (`IChatClient`) with a configurable provider, starting with OpenAI `gpt-6-luna`, replacing Gemini
- Azure App Service hosting

## Planning

Plans live in `openspec/`: current behaviour in `specs/`, planned changes in `changes/`, finished ones in `changes/archive/`. `add-city-lookup` is done. Build the rest in this order: `add-city-movie-search`, `add-movie-links-and-ratings` and `add-observability`, then `deploy-to-azure`. These have a proposal and design only, so write their specs and tasks (as applicable) before building.

## Structure

Vertical slice architecture: code is grouped by feature under `src/Features/`, shared abstractions in `src/Common/Api/`.

- `SearchCities`, `GetFeaturedCities`, `GetMovies`, `GetMoviesByCity`: endpoints, each with a handler
- `GeocodeOpenMeteo`: geocoder client (`ICityGeocoder`)
- Database model, seed data and migrations: `src/Common/Database/`
- `SearchMoviesGemini`: LLM client (`IMovieInfoLlmClient`)
- `SearchMoviesOmdb`: OMDb client (`IMovieInfoClient`)
- Endpoints are registered in `src/Endpoints.cs`; services in `src/ConfigureServices.cs`
- Tests: `tests/UnitTests`, `tests/IntegrationTests`

## Current state

- Flow in place: city -> Gemini suggests movies -> OMDb fills in full metadata -> returned as a list.
- City lookup in place: suggestions from Open-Meteo, featured Cities from Postgres.
- Not started: Movie and FilmingLocation entities, location blurbs, external links and ratings, frontend.

## Commands

- Build: `dotnet build src/server.sln`
- Test: `dotnet test tests/UnitTests`, `dotnet test tests/IntegrationTests`
- New migration: `dotnet ef migrations add <Name> --project src/server.csproj --output-dir Common/Database/Migrations`
