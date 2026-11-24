# Cinema Location

Backend for an app that lists movies by shooting location (city), for travel inspiration. See `Requirements.md` for features, `Entities.md` for the planned data model and `GLOSSARY.md` for domain terms. `Notes.md` holds the author's learning notes and tradeoffs.

The owner is a junior .NET developer building this to learn. Explain non-obvious choices briefly.

## Stack

- .NET 10 ASP.NET Core minimal API (`src/server.csproj`), OpenAPI + Scalar for docs
- Google Gen AI .NET SDK (Gemini) to get movies for a location (chosen over MS Agent Framework for simplicity)
- OMDb API for full movie metadata
- Secrets via ASP.NET Core user secrets
- Tests: xUnit, FluentAssertions, NSubstitute

Planned, not yet in code (see `openspec/changes/`):

- Postgres with EF Core (Npgsql provider 10.0.x; 11.x needs .NET 11), migrations run as a deploy step
- `Microsoft.Extensions.AI` (`IChatClient`) with a configurable provider, starting with OpenAI `gpt-6-luna`, replacing Gemini
- Open-Meteo geocoding to identify Cities; built-in `AddValidation()`; Testcontainers for integration tests
- Azure App Service hosting

## Planning

Plans live in `openspec/`. Build changes in this order: `add-city-lookup`, `add-city-movie-search`, `add-movie-links-and-ratings` and `add-observability`, then `deploy-to-azure`. Only `add-city-lookup` is fully specified.
## Structure

Vertical slice architecture: code is grouped by feature under `src/Features/`, shared abstractions in `src/Common/Api/`.

- `GetMovies`, `GetMoviesByCity`: endpoints, each with a handler
- `SearchMoviesGemini`: LLM client (`IMovieInfoLlmClient`)
- `SearchMoviesOmdb`: OMDb client (`IMovieInfoClient`)
- Endpoints are registered in `src/Endpoints.cs`; services in `src/ConfigureServices.cs`
- Tests: `tests/UnitTests`

## Current state

- Flow in place: city -> Gemini suggests movies -> OMDb fills in full metadata -> returned as a list.
- Not started: database/entities (Movie, City, FilmingLocation), location blurbs, external links and ratings, frontend.

## Commands

- Build: `dotnet build src/server.sln`
- Test: `dotnet test tests/UnitTests`
