# Design

## Context

Based on October 2026 research: `research/links-and-ratings.md` on branch `origin/research/links-and-ratings`. OMDb already returns the three ratings (`imdbRating`, `Metascore`, `Ratings`), but they are not deserialized.

Link formats:

| Site | Link | ID source (Wikidata property) | Fallback search |
|---|---|---|---|
| IMDb | `https://www.imdb.com/title/{imdbId}/` | already stored | not needed |
| Letterboxd | `https://letterboxd.com/imdb/{imdbId}/` (works but undocumented) | P6127 for a direct film link | not needed |
| Rotten Tomatoes | `https://www.rottentomatoes.com/{id}`, such as `m/fight_club` | P1258 | `https://www.rottentomatoes.com/search?search={title}` |
| Metacritic | `https://www.metacritic.com/{id}`, such as `movie/fight-club` | P1712 | `https://www.metacritic.com/search/{title}/` |
| MUBI | `https://mubi.com/films/{id}` | P7299 | `https://mubi.com/en/search/films?query={title}` |

Wikidata is queried by IMDb ID (P345) at `https://query.wikidata.org/sparql`. It needs no key, but requires a descriptive User-Agent and returns 429 with `Retry-After` over its limits. Its data is CC0. OMDb content is CC BY-NC 4.0.

No usable rating API was found for Letterboxd or MUBI. Letterboxd's API is invite-only and excludes personal and LLM projects. IMDb's API is paid, and Rotten Tomatoes' is for licensed partners.

## Goals / Non-Goals

**Goals:** links and ratings without another API key, and without blocking searches.

**Non-Goals:** TMDb, rating refresh, Letterboxd or MUBI ratings.

## Decisions

- **Ratings as nullable Movie columns:** ImdbRating, RottenTomatoes, Metascore. There are three fixed sources, so a table would only add joins.
- **Store site IDs, not URLs:** RottenTomatoesId, MetacriticId, LetterboxdId, MubiId. URLs are built in code, so format changes need no data update.
- **Wikidata as ID bridge:** one query per new City covers all its IMDb IDs. TMDb was rejected because it lacks most of these IDs and requires attribution.
- **Fallback:** use a site's search URL when its ID is missing.
- **Wikidata failure:** save the Movies anyway, with search links where IDs are missing, and do not mark the result partial. There is no backfill: saved results never contact external services. A backfill can be a later change.
- **Wikidata retries:** retry once on a temporary failure or a 429 whose `Retry-After` is at most 5 seconds, with a 10-second timeout per attempt. A failure costs only links, so it should not slow the search much.
- **Only new Movies:** Movies already stored keep their saved IDs and ratings, so only new Movies are looked up.
- **Response:** each movie gains `links`, with `site`, `url`, `rating` (nullable) and `isSearch`, always in the order IMDb, Letterboxd, Rotten Tomatoes, Metacritic, MUBI. `rating` is a display string: `8.8/10`, `79%`, `66/100`. Ratings are stored as numbers, so the format can change without a data update.
- **Letterboxd:** use `/film/{id}/` when Wikidata has P6127, else `/imdb/{imdbId}/`. Both open the film directly, so neither is a search.
- **Rotten Tomatoes:** scores need brand attribution and must link to the movie's own page. A search link is not enough, so the score is stored but returned as null while only a search link exists. Brand attribution is the frontend's job.

## Risks / Trade-offs

- [A shared Movie's ratings date from its first search] → accepted.
- [Movies cached before this change lack the new data] → nothing is deployed yet, so clear development search results.
- [Site URL formats change] → centralise and unit test URL building.

## Open Questions

None. Hiding Rotten Tomatoes scores that have only a search link, and having no backfill, were confirmed with the owner on 2026-11-26.
