# Design

## Context

Builds on City and the test setup from `add-city-lookup`. The current search asks Gemini for movies "shot or set" in a city, sends an item schema as literal text, and fails entirely on one OMDb HTTP error or malformed LLM response. OMDb titles are not URL-encoded, so titles containing `&` or `#` are looked up wrongly; fix and test this.

## Goals / Non-Goals

**Goals:** avoid paying twice for a completed City; switch LLM provider by configuration; never cache an incomplete result as complete; prefer a missing Movie to a wrong one.

**Non-Goals:** background jobs, web search, source citations, refreshing results, admin correction, pre-seeding results (a later script can call the same code), links and ratings, proxy-aware client IPs (`deploy-to-azure`).

## Decisions

### Permanent cache

A non-null `City.GeneratedAt` means complete, including a complete empty result. Cached searches call no external API. Expiry was rejected: it mostly pays for a different list, not a better one. To regenerate, delete the City's FilmingLocations and set `GeneratedAt` to null.

### Data model

- **Movie**: Id; ImdbId (unique); Title; Year; Director, Plot and PosterUrl (nullable, because OMDb returns "N/A"); FetchedAt.
- **FilmingLocation**: CityId + MovieId composite key; Blurb; Position, which preserves LLM order.
- Deletes cascade from City and Movie to FilmingLocation. Movies are shared between Cities, and an existing Movie is reused unchanged.

### Trusted City input

The route takes a GeoNames ID. For an unstored City, the server looks it up by ID in English, so browser text never reaches the prompt and cached names are consistent. Apply the same populated-place filter as suggestions. Open-Meteo answers an unknown ID with 400 and reason "Location ID not found.": map that and filtered places to 404, and other failures to 503.

### Provider-neutral LLM client

One implementation of `IMovieInfoLlmClient` builds the prompt and uses `IChatClient.GetResponseAsync<T>`, which derives the schema from a C# type. `Llm:Provider` and `Llm:Model` select the registration; the key comes from `OPENAI_API_KEY`. Only OpenAI is registered at first, and an unknown provider stops the app at startup. Adding a provider needs a package and a DI registration; prompts need retesting.

Use `gpt-6-luna`, the cheapest model found. On 2026-11-24 the owner confirmed its price ($0.10/$0.50 per million tokens) and that structured output works through the Responses API. The OpenAI account has a $5 hard limit. Compare results for about five well-known cities before filling the database.

### Prompt and filtering

Ask for 15 movies *filmed in* the City, not merely set there. Allow fewer, require a specific place, and ask for a 2-3 line Blurb. Ask for the title exactly as IMDb lists it in English, including accents, plus the year. Identify the City by name, region and country.

Drop entries without a title, year or Blurb. An empty list is valid; unparsable output is an error. Shorten Blurbs over 300 characters at a word boundary.

### OMDb lookup and verification

Owner tests on 2026-11-24 showed that OMDb lists *Amélie* as 2002, so title + 2001 finds nothing. Looser lookups returned different movies, so a successful response does not mean the right one (some tests omitted `type=movie`).

1. Look up title + year, with `type=movie`.
2. If nothing is found, or the result fails verification, look up the title once without the year.
3. Accept only a movie whose title matches, ignoring case, accents and punctuation, and whose year is within 1 of the requested one.
4. Otherwise drop it.

Show OMDb's title and year. Asking the LLM for IMDb IDs was rejected because it can invent them. A fuzzier search was rejected because it raises the risk of wrong matches.

Look up 5 entries at a time, in order, and stop once 10 distinct Movies are confirmed. This avoids unnecessary quota use. OMDb confirms that a movie exists, not where it was filmed.

### OMDb errors

- "Movie not found!" is a normal miss. Any other error response is a failure.
- Retry network errors, timeouts and 5xx twice, after about 0.5s and 1s.
- Do not retry 401, which includes a used-up quota, or other 4xx. After a 401, stop further lookups.
- Use a 5-second timeout per attempt and a 30-second budget for all lookups. Entries unfinished by then count as failures.

### Synchronous flow

Check the cache, take the per-City lock, check again, take a rate-limit permit, identify the City, call the LLM, look up Movies, then save in one transaction. The in-process lock prevents duplicate generation; unique constraints are the backstop. Two Cities can add the same Movie at once, so on a unique violation reload and save once more. Save after the concurrent lookups, since `DbContext` is not parallel-safe. Background jobs were rejected: they add status and recovery for a wait that normally happens once per City.

Allow 60 seconds per LLM attempt. Two attempts plus lookups stay below App Service's 230-second request limit.

### Failures

| Situation | Behaviour |
|---|---|
| Invalid GeoNames ID | 400 |
| Unknown City ID | 404 |
| Geocoder unavailable | 503, save nothing |
| LLM error, timeout or invalid data | retry once, then 503, save nothing |
| OMDb not found or failed verification | drop the movie |
| OMDb failure | return found movies with `partial: true`, save nothing |
| No movies survive, no failures | save the empty result, return 200 |
| Rate limited (uncached only) | 429 with `Retry-After` |

Errors use ProblemDetails. A partial result saves no City row either.

### Rate limit

Allow 20 uncached searches per IP per 60 minutes, because only those cost money and quota. Use a fixed-window limiter from `System.Threading.RateLimiting`, called in the handler. ASP.NET Core's rate-limiting middleware was rejected because it runs before the handler knows whether the result is cached.

Take a permit after the second cache check, inside the per-City lock and before contacting the geocoder. Clients waiting for the same City then use no permit if the first search saves it. Unknown Cities, failures and partial results use a permit, because they contact external services. Remove unused per-City locks, so requests for random IDs do not fill memory. Counts are in memory, so they reset when the app restarts.

### Response

`city`, `partial` and `movies`. Each movie has imdbId, title, year, director, posterUrl, plot and blurb. `add-movie-links-and-ratings` adds links. POST is used because a search can spend money and write data. Response records live in the feature folder; the City part has its own contract.

### Tests

Follow the approach in `add-city-lookup`. Fake `IChatClient` and OMDb HTTP responses, and make delays and timeouts configurable so tests stay fast. Cover concurrency and every failure row.

## Risks / Trade-offs

- [Wrong filming claims are cached permanently] → prompt rules, AI label in the UI, manual regeneration. Not eliminated.
- [Strict verification drops some real movies, especially foreign titles] → accepted; `add-observability` logs drops.
- [Same-title movies within a year of each other can still be confused] → accepted.
- [Partial results cost another LLM call] → accepted; it is cheap with Luna.
- [OMDb allows 1,000 requests/day: at worst 33 new Cities, before retries] → stop early, rate limit, quota handling. A determined visitor can still use up the day's quota.
- [A shared IP, such as an office or mobile network, shares one rate limit] → accepted at low traffic.
- [The lock and limiter work on one instance only] → accepted on a single App Service instance.
- [A client disconnecting can waste an LLM call] → accepted.

## Migration Plan

Add the Movie and FilmingLocation migration, which leaves City data unchanged. Set `OPENAI_API_KEY`. Rollback: revert the code and the migration, losing cached results.
