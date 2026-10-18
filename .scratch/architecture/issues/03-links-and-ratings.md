# What external links and ratings can we actually get?

Type: research
Status: resolved
Blocked by: 

## Question

Requirements want links to IMDb, Rotten Tomatoes, MUBI, Letterboxd etc., with ratings where available. OMDb returns IMDb/Rotten Tomatoes/Metacritic ratings. For each target site: is there an API or a stable URL scheme (e.g. by IMDb ID, TMDb ID, slug, search URL)? Which ratings are available, under what terms/rate limits/cost? Is TMDb worth adding as an ID bridge?

## Answer

- Ratings: OMDb is the only practical source. It already returns IMDb rating, Rotten Tomatoes Tomatometer and Metascore (`Ratings`, `imdbRating`, `Metascore`); we just don't deserialize them yet. Free key allows 1,000 requests/day.
- No usable official APIs. IMDb's API is paid (AWS Data Exchange). RT is for licensed partners only, and any RT score we show must link back to its RT page. Letterboxd's API is invite-only and excludes personal and LLM projects. MUBI and Metacritic have no public API.
- Links: `imdb.com/title/{imdbID}/` and `letterboxd.com/imdb/{imdbID}/` (redirect, works but undocumented; `/tmdb/{id}` is the documented form) work from the ID we already have. RT, Metacritic and MUBI use slugs or IDs we can't derive from the IMDb ID.
- Wikidata beats TMDb as the ID bridge: one free SPARQL query by IMDb ID (CC0, no key) returns TMDb, RT, Metacritic, Letterboxd and MUBI IDs. Store them on Movie, and fall back to each site's search URL when an ID is missing.
- TMDb isn't needed now. Its external IDs only cover IMDb, Wikidata and social accounts, it requires attribution, and it only adds its own rating. Revisit it for posters or richer metadata.

Findings: branch `research/links-and-ratings`, file `research/links-and-ratings.md`
