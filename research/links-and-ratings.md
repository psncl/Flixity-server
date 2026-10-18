# External links and ratings: what we can actually get

Question (ticket 03): for IMDb, Rotten Tomatoes, MUBI, Letterboxd (and Metacritic), is there an official API or a stable URL scheme, which ratings are available and on what terms, and is TMDb worth adding as an ID bridge?

Researched 2026-10-18. URL schemes marked "verified" were checked with an HTTP request against Fight Club (IMDb `tt0137523`, TMDb `550`).

## Summary table

| Site | Official API for us? | Stable link from data we have | Ratings we can show |
|---|---|---|---|
| IMDb | Paid only (AWS Data Exchange); free bulk TSVs for non-commercial use | `https://www.imdb.com/title/{imdbID}/`, using the ID OMDb already returns | IMDb rating via OMDb (already have it), or the free `title.ratings` dataset |
| Rotten Tomatoes | No. Licensed partners only (Business Proposal Form) | Slug such as `m/fight_club`, not derivable from the IMDb ID. Get it from Wikidata P1258, or use a search URL | Tomatometer via OMDb (no audience score). Must follow RT attribution and link-back rules |
| Metacritic | No public API found | Slug such as `movie/fight-club`. Get it from Wikidata P1712, or use a search URL | Metascore via OMDb |
| Letterboxd | API is invite-only, and personal or LLM projects are excluded | `https://letterboxd.com/imdb/{imdbID}/` or `/tmdb/{tmdbId}/` redirect to the film page (verified) | None we can legitimately get |
| MUBI | No public API | `https://mubi.com/films/{mubiNumericId}` (verified redirect). Get the ID from Wikidata P7299, or use a search URL | None |
| TMDb | Yes, free for non-commercial use with attribution | `https://www.themoviedb.org/movie/{tmdbId}` | TMDb's own `vote_average` |

## Per site

### OMDb (current source)
- We already call OMDb by title and year. The response also includes `imdbRating`, `Metascore` and a `Ratings` array with Internet Movie Database, Rotten Tomatoes and Metacritic entries. Our `OmdbApiResponse` record (`src/Features/SearchMoviesOmdb/MovieInfoOmdbApiClient.cs`) just doesn't deserialize those fields yet. Note: the docs page doesn't list the response fields. This comes from OMDb's actual responses and from the ticket's premise.
- Free key: "FREE! (1,000 daily limit)". Paid access goes through Patreon, and the Poster API is patrons only. https://www.omdbapi.com/apikey.aspx and https://www.omdbapi.com/
- Content is licensed CC BY-NC 4.0, and OMDb "is not endorsed by or affiliated with IMDb.com". https://www.omdbapi.com/

### IMDb
- The official IMDb API (GraphQL) and bulk data are "available exclusively via AWS Data Exchange". They're sold as products (IMDb Ratings, Essential Metadata and others) with no public prices, so you have to contact IMDb. https://data.imdb.com/
- Free non-commercial datasets include `title.ratings.tsv.gz` (`tconst`, `averageRating`, `numVotes`), refreshed daily, for "personal and non-commercial use". https://data.imdb.com/non-commercial-datasets/
- Link: `https://www.imdb.com/title/{imdbID}/` is IMDb's canonical title URL (it's also the formatter URL on Wikidata property P345: https://www.wikidata.org/wiki/Property:P345). We already store `imdbID`, so this link costs nothing.

### Rotten Tomatoes
- No self-serve API. To integrate RT data "via APIs, data feeds" you submit a Business Proposal Form. The same page sets usage rules: brand attribution with the ® mark, Fresh or Rotten logos only for the matching score ranges (60% and up, 59% and below), and "Scores and reviews must link back to the corresponding movie pages on rottentomatoes.com". https://www.rottentomatoes.com/help_desk/licensing
- Third-party reports put the licence at around $60k a year. That figure isn't official, so it isn't relied on here.
- Link: RT uses slugs (`https://www.rottentomatoes.com/m/fight_club`, verified 200) that can't be derived from the IMDb ID. You can get the slug from Wikidata P1258 (formatter `https://www.rottentomatoes.com/$1`, e.g. `m/fight_club`) or fall back to `https://www.rottentomatoes.com/search?search={title}`. https://www.wikidata.org/wiki/Property:P1258
- Rating: the Tomatometer from OMDb is the only practical source. Because of the link-back rule, any displayed score has to link to the RT movie page, so in practice we need the slug and a search URL isn't enough.

### Metacritic
- No public developer API found. Metascore comes via OMDb.
- Link: slug, e.g. `https://www.metacritic.com/movie/fight-club/` (verified 200). Get it from Wikidata P1712 (`movie/fight-club`) or use the search URL `https://www.metacritic.com/search/{title}/`. https://www.wikidata.org/wiki/Property:P1712

### Letterboxd
- Film data on Letterboxd comes from TMDB. `https://letterboxd.com/tmdb/{id}` is documented: it imports the film if needed and then redirects to the film page. https://letterboxd.com/about/film-data/
- `https://letterboxd.com/imdb/{imdbID}/` isn't documented on that page, but it works: `/imdb/tt0137523/` returns 302 to `/film/fight-club/`, the same as `/tmdb/550/`. Prefer the documented `/tmdb/` form if we have TMDb IDs. Otherwise `/imdb/` works with the data we already have, but it could change without notice.
- API: access is "by request only". Letterboxd is not currently approving projects focused on data analysis, visualization or recommendations, LLM or GPT use, private or personal projects, or anything that recreates paid-tier features, and it points metadata use cases to TMDB. https://letterboxd.com/api-beta/ (behind Cloudflare, so the content was read through a search-engine snapshot). An LLM-based personal learning project fits several of those exclusions, so there are no Letterboxd ratings for us.

### MUBI
- No public or partner developer API found. The only programmatic access is unofficial reverse-engineering of MUBI's app API, which we shouldn't use.
- Link: `https://mubi.com/films/{numericId}` redirects to `/en/films/{id}` and then a localised `/en/gb/films/{id}` (verified). The numeric ID comes from Wikidata P7299 (Fight Club = `918`). https://www.wikidata.org/wiki/Property:P7299. Fallback: `https://mubi.com/en/search/films?query={title}` (returns 200).
- Ratings: none available legitimately.

### TMDb (candidate ID bridge)
- `GET /3/movie/{movie_id}/external_ids` returns `imdb_id`, `wikidata_id` and social IDs. https://developer.themoviedb.org/reference/movie-external-ids
- `GET /3/find/{external_id}?external_source=imdb_id` maps an IMDb ID to a TMDb movie, and the result includes `vote_average`. https://developer.themoviedb.org/reference/find-by-id
- Terms: free for non-commercial use with attribution. That means a TMDB logo less prominent than ours, the notice "This product uses the TMDB API but is not endorsed or certified by TMDB", and credit in an About or Credits section. Commercial use needs a licence. https://developer.themoviedb.org/docs/faq
- Rate limit: about 40 requests per second, so respect `429`. https://developer.themoviedb.org/docs/rate-limiting
- What TMDb adds for links: only the documented Letterboxd `/tmdb/` URL, a TMDb link and TMDb's own rating. It does **not** give RT, Metacritic or MUBI IDs (external IDs cover IMDb, Wikidata and social accounts only).

### Wikidata (alternative bridge)
- A single SPARQL query keyed on the IMDb ID (P345) returns the TMDb (P4947), Rotten Tomatoes (P1258), Metacritic (P1712), Letterboxd (P6127) and MUBI (P7299) IDs. Each property has a formatter URL. For `tt0137523` it returned `rt=m/fight_club`, `mc=movie/fight-club`, `lb=fight-club`, `mubi=918`, `tmdb=550` (verified).
- Public endpoint `https://query.wikidata.org/sparql`: no key needed. Limits are 60s runtime per query, 60s of processing per minute per client, 5 parallel queries per IP, and 429 with `Retry-After` when exceeded. It requires a descriptive `User-Agent`. https://www.mediawiki.org/wiki/Wikidata_Query_Service/User_Manual
- Data is CC0 (https://www.wikidata.org/wiki/Wikidata:Licensing). Coverage is community-maintained, so obscure films may be missing IDs and we need the search-URL fallback.

## Recommendation

1. **Ratings: use only OMDb.** Extend `OmdbApiResponse` with `Ratings`, `imdbRating` and `Metascore`, and store IMDb, RT and Metacritic values. Don't pursue Letterboxd, MUBI or RT APIs: they're closed, paid or exclude this kind of project. Show RT scores with RT attribution and a link to the RT movie page.
2. **Links: build them from `imdbID` plus Wikidata.** IMDb and Letterboxd (`/imdb/{id}/`) work today with no new dependency. For RT, Metacritic and MUBI, do one Wikidata SPARQL lookup by IMDb ID when a movie is first saved and store the slugs and IDs on the Movie entity (e.g. a `MovieExternalIds` value object). Fall back to each site's search URL when an ID is missing.
3. **TMDb: not needed now.** It only adds the documented Letterboxd `/tmdb/` link and a TMDb rating, and it brings attribution obligations. Wikidata also returns the TMDb ID for free, so we can store it and switch the Letterboxd link to `/tmdb/{id}` without calling TMDb. Revisit TMDb if we later want posters, better metadata or a replacement for OMDb's 1,000/day limit.
