# Tasks

## 1. Data model

- [x] 1.1 Add nullable ImdbRating, RottenTomatoes, Metascore, RottenTomatoesId, MetacriticId, LetterboxdId and MubiId to Movie, plus a migration that also deletes development search results (FilmingLocations and Movies, with GeneratedAt reset); verify it applies to a database with saved results and leaves the seeded Cities intact
- [x] 1.2 Check `Entities.md` matches the final columns; verify an integration test saves and reloads a Movie with and without the new values

## 2. Ratings from OMDb

- [x] 2.1 Deserialize `imdbRating`, `Metascore` and the Rotten Tomatoes entry in `Ratings`, mapping "N/A" and unreadable values to null; verify unit tests with fake OMDb responses for all present, each missing, and unreadable values

## 3. Site IDs from Wikidata

- [ ] 3.1 Add a Wikidata client (its own slice and interface) that sends one SPARQL query for a list of IMDb IDs and returns their Rotten Tomatoes, Metacritic, Letterboxd and MUBI IDs, with a descriptive User-Agent and no key; verify unit tests with fake HTTP responses for found, partly found and unknown IDs, and that the request carries the User-Agent and every IMDb ID
- [ ] 3.2 Retry once on a temporary failure or a 429 with `Retry-After` of at most 5 seconds, with a 10-second timeout per attempt, and report failure as an empty result; verify unit tests for failure then success, two failures, a timeout, and a 429 with a long `Retry-After` that is not retried
- [ ] 3.3 Register the client and its settings, and document Wikidata in `README.md` and `CLAUDE.md`; verify the app starts with the documented setup

## 4. Links in the search

- [ ] 4.1 Build the five links from a Movie's stored IDs and ratings in one place, with search fallbacks, title encoding, rating formats and the hidden Rotten Tomatoes score; verify unit tests for every link and rating scenario in the spec
- [ ] 4.2 Look up site IDs for new Movies only, after verification and before saving, and save the ratings and IDs with them; verify integration tests for a first search with IDs, a shared Movie that is not looked up again, and an unavailable ID source that still saves a result that is not partial
- [ ] 4.3 Add `links` to the response records for first and saved results; verify integration tests that the response has five links in order and that a saved result returns the same links with every external service failing
- [ ] 4.4 Update `server.http` and `CLAUDE.md` current state; verify the OpenAPI document in Scalar shows `links`

## 5. Integration check

- [ ] 5.1 Run all tests, then search two Cities not yet saved using the real services; verify with the owner that direct links open the right pages and search links find the Movie
