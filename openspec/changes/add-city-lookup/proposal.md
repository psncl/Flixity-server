# Proposal

## Why

Movies are cached permanently per City, so free text such as "paris" must resolve to exactly one City before any paid search. The app has no database and no notion of City identity yet.

## What Changes

- Add a persistent store for Cities, identified by their GeoNames ID.
- Add `GET /cities?query=` returning matching Cities for the user to pick from.
- Add `GET /cities/featured` returning curated UK and European Cities.
- Report invalid input and upstream failures as ProblemDetails.
- Leave existing movie endpoints unchanged; `add-city-movie-search` removes them.

## Capabilities

### New Capabilities
- `city-lookup`: finding and identifying Cities, from typed text or the featured list.

### Modified Capabilities

None.

## Impact

- New dependencies: Postgres, EF Core, the Open-Meteo geocoding API, Testcontainers (tests; needs Docker).
- New feature slices under `src/Features/`, registrations in `src/ConfigureServices.cs` and `src/Endpoints.cs`.
- The frontend must show Open-Meteo attribution and require picking a suggestion; pressing Enter on unmatched text does not search.
