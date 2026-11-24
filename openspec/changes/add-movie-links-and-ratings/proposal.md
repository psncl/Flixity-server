# Proposal

## Why

Users should be able to open a Movie on IMDb, Rotten Tomatoes, Metacritic, MUBI and Letterboxd, and see ratings where available.

## What Changes

- Add links to each Movie, marking those that fall back to a site search.
- Add IMDb, Rotten Tomatoes and Metacritic ratings where available.
- Save ratings when the Movie is first added; they are never refreshed.
- Keep searches working when link lookup fails.

## Capabilities

### New Capabilities
- `movie-links-and-ratings`: external links and ratings shown with a Movie.

### Modified Capabilities

None yet. If `city-movie-search` is archived first, check whether its response requirement needs a delta.

## Impact

- Depends on `add-city-movie-search`.
- New external dependency: Wikidata (no key). Ratings come from existing OMDb responses.
- Adds columns to Movie.
