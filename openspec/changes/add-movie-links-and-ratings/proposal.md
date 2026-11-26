# Proposal

## Why

Users should be able to open a Movie on IMDb, Rotten Tomatoes, Metacritic, MUBI and Letterboxd, and see ratings where available.

## What Changes

- Add links to each Movie, marking those that fall back to a site search.
- Add IMDb, Rotten Tomatoes and Metacritic ratings where available.
- Save ratings when the Movie is first added; they are never refreshed.
- Keep searches working when link lookup fails; missing IDs fall back to search links and are not backfilled.
- Hide Rotten Tomatoes scores that have only a search link.

## Capabilities

### New Capabilities
- `movie-links-and-ratings`: external links and ratings shown with a Movie.

### Modified Capabilities

None. `city-movie-search` is archived and its requirements still hold: `links` only adds to each Movie, and its "Shared Movie" scenario already keeps existing metadata unchanged. The new behaviour lives in `movie-links-and-ratings`.

## Impact

- Depends on `add-city-movie-search`.
- New external dependency: Wikidata (no key). Ratings come from existing OMDb responses.
- Adds columns to Movie.
