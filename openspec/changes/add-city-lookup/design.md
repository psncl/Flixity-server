# Design

## Context

.NET 10 minimal API using vertical slices, currently with no persistence. See `proposal.md` for motivation. Postgres, EF Core and Azure App Service are fixed choices. This supersedes an earlier choice of Dapper and DbUp.

## Goals / Non-Goals

**Goals:** stable City identity; persistence and test foundations that later changes reuse.

**Non-Goals:** movie tables, caching suggestions, regions as search targets, deployment.

## Decisions

### City table

| Column | Notes |
|---|---|
| Id | integer primary key |
| GeoNamesId | unique |
| Name, Country, CountryCode | required |
| Region | nullable |
| IsFeatured | marks curated Cities |
| GeneratedAt | nullable; null means never searched. Written by `add-city-movie-search` |

`Name + Country` was rejected as a key: several US cities named Springfield would collide. Our own key keeps foreign keys small and the geocoder replaceable.

### Open-Meteo geocoding behind `ICityGeocoder`

It needs no key, returns GeoNames IDs and supports localized names. Alternatives: string rules cannot detect ambiguity; an LLM tends to guess.

Checked on 2026-11-24:
- `/v1/search` omits `results` entirely when nothing matches.
- `language=fr` finds London for "Londres"; English does not.
- Populated places in the sampled results have feature codes beginning with `PPL`; filter on that prefix.
- `/v1/get?id=` returns one place. An unknown ID returns **400** with reason "Location ID not found.", so `add-city-movie-search` must map that to 404.

Request more than 10 results, then filter, so filtering does not shorten the list unnecessarily.

### EF Core, using `DbContext` directly in handlers

`DbContext` is already a unit of work, so a repository layer adds little. Use the Npgsql provider **10.0.x**; 11.0.0 requires .NET 11. A `DbContext` cannot run operations in parallel.

### Migrations outside app startup

Development uses `dotnet ef database update`. Production uses a reviewed script or bundle as a deploy step. Startup migration was rejected: the app would need schema permissions, and a bad migration could stop it starting.

### Featured Cities are seeded rows

They are seeded through migrations, avoiding a second list to maintain. The owner approved these 30 on 2026-11-24. IDs were looked up the same day:

| Country | City and GeoNames ID |
|---|---|
| GB | London 2643743, Edinburgh 2650225, Glasgow 2648579, Manchester 2643123, Liverpool 2644210, Bath 2656173, Oxford 2640729, Belfast 2655984 |
| IE | Dublin 2964574 |
| FR | Paris 2988507, Nice 2990440 |
| IT | Rome 3169070, Venice 3164603, Florence 3176959 |
| ES | Madrid 3117735, Barcelona 3128760, Seville 2510911 |
| DE | Berlin 2950159, Munich 2867714 |
| PT | Lisbon 2267057 |
| NL | Amsterdam 2759794 |
| BE | Bruges 2800931 |
| AT | Vienna 2761369, Salzburg 2766824 |
| CZ | Prague 3067696 |
| HU | Budapest 3054643 |
| DK | Copenhagen 2618425 |
| SE | Stockholm 2673730 |
| GR | Athens 264371 |
| HR | Dubrovnik 3201047 |

### Built-in validation

Use .NET 10 `AddValidation()` and `AddProblemDetails()`. Trimming must happen before the length check. Make missing-parameter failures produce ProblemDetails in every environment, since binding failures bypass validation. FluentValidation was rejected for now because inputs are simple; reconsider for complex bodies or cross-field rules.

### Tests

- Unit: xUnit, FluentAssertions, NSubstitute. Fake the HTTP response for the geocoder client so URL and JSON handling are tested.
- Integration: `WebApplicationFactory` with Testcontainers Postgres. EF's in-memory provider does not enforce relational constraints.
- Tests never call real external APIs.
- FluentAssertions 8 is free only for non-commercial use. Keep it while that holds; AwesomeAssertions is an Apache-2.0 alternative.

## Risks / Trade-offs

- [Open-Meteo's free tier is non-commercial, under 10,000 calls/day, and may count shared App Service IPs together] → debounce in the frontend, return 503 on quota errors, keep it behind an interface.
- [Tiny villages clutter suggestions] → consider a population minimum once results can be seen.
- [Non-English matching is incomplete] → pass the browser language.
- [Integration tests need Docker] → keep them in a separate project from unit tests.

## Migration Plan

Create the initial migration and apply it to an empty database. Rollback: revert the code and drop the new database.

## Open Questions

- Whether to add a population minimum, and its value. The first version has none.
