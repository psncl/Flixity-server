# Design

## Context

Builds on City and the test setup from `add-city-lookup`. The current search asks Gemini for movies "shot or set" in a city, sends an item schema as literal text, and fails entirely on one OMDb HTTP error or malformed LLM response. OMDb titles are not URL-encoded, so titles containing `&` or `#` are looked up wrongly; fix and test this.

## Goals / Non-Goals

**Goals:** avoid paying twice for a completed City; switch LLM provider by configuration; never cache an incomplete result as complete.

**Non-Goals:** background jobs, web search, source citations, refreshing results, admin correction, pre-seeding results (a later script can call the same code).

## Decisions

### Permanent cache

A non-null `City.GeneratedAt` means complete, including a complete empty result. Cached searches call no external API. Expiry was rejected: it mostly pays for a different list, not a better one. To regenerate, delete the City's FilmingLocations and set `GeneratedAt` to null.

### Data model

- **Movie**: Id; ImdbId (unique); Title; Year; Director; Plot and PosterUrl (nullable); FetchedAt.
- **FilmingLocation**: CityId + MovieId composite key; Blurb; Position, which preserves LLM order.
- Deletes cascade from City and Movie to FilmingLocation. Movies are shared between Cities.

### Trusted City input

The route takes a GeoNames ID. For an unstored City, the server looks up the name by ID, so browser text never reaches the prompt. Use the English name so cached City names are consistent. Map Open-Meteo's 400 "Location ID not found." to 404.

### Provider-neutral LLM client

One implementation of `IMovieInfoLlmClient` builds the prompt and uses `IChatClient.GetResponseAsync<T>`, which derives the schema from a C# type. `Llm:Provider` and `Llm:Model` select the registration. Only OpenAI is registered at first. Adding a provider needs a package and a DI registration; prompts need retesting.

Start with `gpt-6-luna`, the cheapest model found. Compare its results for about five well-known cities before filling the database.

### Prompt and filtering

Ask for 15 movies *filmed in* the City, not merely set there. Allow fewer, require a specific place, and ask for a 2-3 line Blurb. Keep at most 10 that OMDb confirms, in order, without duplicate IMDb IDs. OMDb confirms that a movie exists, not where it was filmed.

### Synchronous flow

LLM call, then about 5 concurrent OMDb lookups, then one transaction. A per-City in-process lock prevents duplicate generation; unique constraints are the backstop. Background jobs were rejected: they add status and recovery for a wait that normally happens once per City. Save after the concurrent lookups, since `DbContext` is not parallel-safe.

### Failures

| Situation | Behaviour |
|---|---|
| Invalid GeoNames ID | 400 |
| Unknown City ID | 404 |
| Geocoder unavailable | 503, save nothing |
| LLM error or invalid data | retry once, then 503, save nothing |
| OMDb not found | drop the movie |
| OMDb error or quota | retry, then return found movies with `partial: true`, save nothing |
| No movies survive, no errors | save the empty result, return 200 |
| Rate limited | 429 with `Retry-After` |

Errors use ProblemDetails.

### Response

`city`, `partial` and `movies`. Each movie has imdbId, title, year, director, posterUrl, plot and blurb. `add-movie-links-and-ratings` adds links. POST is used because a search can spend money and write data.

### Tests

Follow the approach in `add-city-lookup`. Fake `IChatClient` and OMDb HTTP responses. Cover concurrency and every failure row.

## Risks / Trade-offs

- [Wrong filming claims are cached permanently] → prompt rules, AI label in the UI, manual regeneration. Not eliminated.
- [Partial results cost another LLM call] → accepted; it is cheap with Luna.
- [OMDb allows 1,000 requests/day, about 66 new Cities] → rate limiting and quota handling.
- [The lock works on one instance only] → accepted on a single App Service instance.

## Open Questions

- Rate-limit values.
- Verify the Luna model ID, price and structured-output support on OpenAI's own pages before writing specs. If unsupported, choose the cheapest model that supports it.
- OMDb retry count and delay.
