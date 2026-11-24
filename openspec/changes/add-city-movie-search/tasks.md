# Tasks

## 1. Data model

- [ ] 1.1 Add Movie and FilmingLocation with keys, cascades and nullable metadata, plus a migration; verify it applies to a database containing the seeded Cities and leaves them unchanged
- [ ] 1.2 Update `Entities.md` (nullable Director); verify integration tests for unique ImdbId, one FilmingLocation per City and Movie, and cascade deletes that keep shared Movies

## 2. City lookup by ID

- [ ] 2.1 Add lookup by GeoNames ID to `ICityGeocoder`, in English and with the populated-place filter; verify unit tests for found, filtered, unknown ID and geocoder failure

## 3. LLM client

- [ ] 3.1 Replace `Google.GenAI` with `Microsoft.Extensions.AI` and its OpenAI adapter, and select provider and model from configuration; verify tests for a changed model and an unsupported provider
- [ ] 3.2 Rewrite `IMovieInfoLlmClient` with the filmed-in prompt, a typed response and provider-neutral names; verify unit tests for prompt content, dropped entries, Blurb shortening and an empty list
- [ ] 3.3 Retry once with a 60-second timeout per attempt; verify unit tests for failure, timeout and unusable data, each followed by success and by a second failure
- [ ] 3.4 Update `README.md` and `CLAUDE.md` for the new key and settings; verify the documented setup starts the app

## 4. OMDb client

- [ ] 4.1 Encode titles, add poster, map "N/A" to null and parse years safely; verify unit tests with fake HTTP responses, including `&` and `#`
- [ ] 4.2 Distinguish not found, temporary failure and refusal, with two retries and a timeout; verify unit tests for each, including no retry after 401
- [ ] 4.3 Add title and year verification with one lookup without the year; verify unit tests for every verification scenario in the spec

## 5. Search endpoint

- [ ] 5.1 Add batched lookups that stop at 10 distinct Movies, after a 401, or at the 30-second budget; verify unit tests for order, duplicates, early stop, quota and budget
- [ ] 5.2 Add `POST /cities/{geoNamesId}/search` with its response records for a first search, saving in one transaction; verify integration tests for response shape, new and stored Cities, and shared Movies
- [ ] 5.3 Serve completed results from the database; verify integration tests with all external services failing, including a complete empty result
- [ ] 5.4 Handle partial results and all failures; verify integration tests for 400, 404, 503, partial and fully unavailable metadata, checking that nothing is saved
- [ ] 5.5 Add the per-City lock and the retry after a unique violation; verify integration tests for both simultaneous-search scenarios

## 6. Rate limit

- [ ] 6.1 Limit uncached searches to 20 per IP per 60 minutes with a ProblemDetails 429 and `Retry-After`; verify integration tests for every rate-limit scenario, including unlimited cached searches and waiting searches

## 7. Remove the old search

- [ ] 7.1 Remove the `GetMovies`, `GetMoviesByCity` and `SearchMoviesGemini` slices, their tests and their `server.http` request; verify both old routes return 404 and the solution builds
- [ ] 7.2 Update `CLAUDE.md` structure and current state; verify no reference to the removed code remains in source or docs other than notes and archived plans

## 8. Integration check

- [ ] 8.1 Run all tests, then search five well-known cities chosen with the owner using the real services; verify repeat searches come from the database, and review filming claims and dropped titles with the owner
